using System.Data;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Models;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Data.Utils;

namespace OpinionesClientes.Data.Repositories;

/// <summary>
/// Carga las dimensiones en OpinionesDW.
/// Pasos: SqlBulkCopy a una tabla temporal → MERGE contra la dimensión → conteo de inserciones y actualizaciones.
/// Las llaves sustitutas existentes no cambian (SCD tipo 1), así la tabla de hechos sigue siendo válida.
/// </summary>
public partial class SqlDataWarehouseRepository : IDataWarehouseRepository
{
    private readonly string _cadenaConexion;
    private readonly CargaDimensionesOptions _opciones;
    private readonly ILogger<SqlDataWarehouseRepository> _logger;

    public SqlDataWarehouseRepository(IConfiguration configuracion, IOptions<CargaDimensionesOptions> opciones,
        ILogger<SqlDataWarehouseRepository> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
        _cadenaConexion = configuracion.GetConnectionString(_opciones.ConexionDataWarehouse)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{_opciones.ConexionDataWarehouse}'.");
    }

    public async Task<ResultadoMerge> GuardarDimensionAsync<TDim>(string tablaDimension, IReadOnlyCollection<TDim> filas,
        IReadOnlyCollection<string> columnasLlave, CancellationToken cancellationToken) where TDim : class
    {
        ValidarNombreTabla(tablaDimension);

        var propiedades = ObjectMapper.Propiedades(typeof(TDim));
        var columnas = propiedades.Select(p => p.Name).ToList();
        if (columnasLlave.Count == 0 || columnasLlave.Any(l => !columnas.Contains(l)))
            throw new ArgumentException($"Las columnas llave de {tablaDimension} no coinciden con las propiedades de {typeof(TDim).Name}.");

        using var datos = CrearDataTable(filas, propiedades);

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);

        // Todo en una transacción: si algo falla, la dimensión queda como estaba
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        // 1. Tabla temporal con la misma estructura de columnas que la dimensión
        var listaColumnas = string.Join(", ", columnas.Select(c => $"[{c}]"));
        await using (var crear = new SqlCommand(
            $"SELECT TOP (0) {listaColumnas} INTO #origen FROM {tablaDimension};", conexion, transaccion))
        {
            await crear.ExecuteNonQueryAsync(cancellationToken);
        }

        // 2. Carga masiva a la temporal
        using (var bulk = new SqlBulkCopy(conexion, SqlBulkCopyOptions.Default, transaccion))
        {
            bulk.DestinationTableName = "#origen";
            bulk.BulkCopyTimeout = _opciones.TimeoutSegundos;
            foreach (DataColumn columna in datos.Columns)
                bulk.ColumnMappings.Add(columna.ColumnName, columna.ColumnName);
            await bulk.WriteToServerAsync(datos, cancellationToken);
        }

        // 3. MERGE: inserta lo nuevo y actualiza solo lo que cambió
        var sql = ConstruirMerge(tablaDimension, propiedades, columnasLlave);
        await using var merge = new SqlCommand(sql, conexion, transaccion) { CommandTimeout = _opciones.TimeoutSegundos };
        await using var lector = await merge.ExecuteReaderAsync(cancellationToken);

        int insertados = 0, actualizados = 0, total = 0;
        if (await lector.ReadAsync(cancellationToken))
        {
            insertados = lector.GetInt32(0);
            actualizados = lector.GetInt32(1);
            total = lector.GetInt32(2);
        }
        await lector.CloseAsync();
        await transaccion.CommitAsync(cancellationToken);

        var sinCambios = filas.Count - insertados - actualizados;
        _logger.LogDebug("{Tabla}: {Ins} insertados, {Act} actualizados, {Igual} sin cambios",
            tablaDimension, insertados, actualizados, sinCambios);

        return new ResultadoMerge(insertados, actualizados, sinCambios, total);
    }

    public async Task RegistrarResultadoAsync(ResultadoCargaDimension r, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.LogCargaDimensiones
                (IdLote, Dimension, Modo, TablaOrigen, RegistrosLeidos, RegistrosDescartados,
                 Insertados, Actualizados, TotalEnDimension, DuracionMs, Estado, MensajeError, FechaInicio, FechaFin)
            VALUES
                (@IdLote, @Dimension, @Modo, @TablaOrigen, @Leidos, @Descartados,
                 @Insertados, @Actualizados, @Total, @Duracion, @Estado, @Error, @Inicio, @Fin);
            """;

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);
        await using var comando = new SqlCommand(sql, conexion);

        comando.Parameters.Add("@IdLote", SqlDbType.UniqueIdentifier).Value = r.IdLote;
        comando.Parameters.Add("@Dimension", SqlDbType.NVarChar, 50).Value = r.Dimension;
        comando.Parameters.Add("@Modo", SqlDbType.NVarChar, 20).Value = "Upsert";
        comando.Parameters.Add("@TablaOrigen", SqlDbType.NVarChar, 200).Value = Recortar(r.TablaOrigen, 200);
        comando.Parameters.Add("@Leidos", SqlDbType.Int).Value = r.RegistrosLeidos;
        comando.Parameters.Add("@Descartados", SqlDbType.Int).Value = r.RegistrosDescartados;
        comando.Parameters.Add("@Insertados", SqlDbType.Int).Value = r.Insertados;
        comando.Parameters.Add("@Actualizados", SqlDbType.Int).Value = r.Actualizados;
        comando.Parameters.Add("@Total", SqlDbType.Int).Value = r.TotalEnDimension;
        comando.Parameters.Add("@Duracion", SqlDbType.BigInt).Value = r.DuracionMs;
        comando.Parameters.Add("@Estado", SqlDbType.NVarChar, 10).Value = r.Estado;
        comando.Parameters.Add("@Error", SqlDbType.NVarChar, 1000).Value =
            (object?)Recortar(r.MensajeError, 1000) ?? DBNull.Value;
        comando.Parameters.Add("@Inicio", SqlDbType.DateTime2).Value = r.FechaInicio;
        comando.Parameters.Add("@Fin", SqlDbType.DateTime2).Value = r.FechaFin;

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Arma el MERGE a partir de las propiedades de la entidad.
    /// Las llaves que aceptan null (IdClienteOrigen del miembro Anónimo) se comparan con IS NULL.
    /// El EXCEPT detecta cambios reales aunque haya valores null.
    /// </summary>
    internal static string ConstruirMerge(string tabla, PropertyInfo[] propiedades, IReadOnlyCollection<string> llaves)
    {
        var columnas = propiedades.Select(p => p.Name).ToList();
        var noLlave = columnas.Where(c => !llaves.Contains(c)).ToList();

        var condicion = string.Join(" AND ", propiedades.Where(p => llaves.Contains(p.Name)).Select(p =>
            Nullable.GetUnderlyingType(p.PropertyType) is null
                ? $"t.[{p.Name}] = s.[{p.Name}]"
                : $"(t.[{p.Name}] = s.[{p.Name}] OR (t.[{p.Name}] IS NULL AND s.[{p.Name}] IS NULL))"));

        var sb = new StringBuilder();
        sb.AppendLine("DECLARE @acciones TABLE (Accion NVARCHAR(10));");
        sb.AppendLine($"MERGE {tabla} WITH (HOLDLOCK) AS t");
        sb.AppendLine("USING #origen AS s");
        sb.AppendLine($"    ON {condicion}");

        if (noLlave.Count > 0)
        {
            sb.AppendLine($"WHEN MATCHED AND EXISTS (SELECT {Lista("s", noLlave)} EXCEPT SELECT {Lista("t", noLlave)}) THEN");
            sb.AppendLine($"    UPDATE SET {string.Join(", ", noLlave.Select(c => $"t.[{c}] = s.[{c}]"))}");
        }

        sb.AppendLine("WHEN NOT MATCHED BY TARGET THEN");
        sb.AppendLine($"    INSERT ({string.Join(", ", columnas.Select(c => $"[{c}]"))})");
        sb.AppendLine($"    VALUES ({Lista("s", columnas)})");
        sb.AppendLine("OUTPUT $action INTO @acciones;");
        sb.AppendLine("SELECT (SELECT COUNT(*) FROM @acciones WHERE Accion = N'INSERT'),");
        sb.AppendLine("       (SELECT COUNT(*) FROM @acciones WHERE Accion = N'UPDATE'),");
        sb.AppendLine($"       (SELECT COUNT(*) FROM {tabla});");
        return sb.ToString();

        static string Lista(string alias, IEnumerable<string> cols) => string.Join(", ", cols.Select(c => $"{alias}.[{c}]"));
    }

    private static DataTable CrearDataTable<TDim>(IEnumerable<TDim> filas, PropertyInfo[] propiedades)
    {
        var tabla = new DataTable(typeof(TDim).Name);
        foreach (var p in propiedades)
            tabla.Columns.Add(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType);

        foreach (var fila in filas)
        {
            var nueva = tabla.NewRow();
            foreach (var p in propiedades)
                nueva[p.Name] = p.GetValue(fila) ?? DBNull.Value;
            tabla.Rows.Add(nueva);
        }
        return tabla;
    }

    /// <summary>SEGURIDAD: solo se aceptan tablas de dimensión del esquema dbo (dbo.Dim_Algo).</summary>
    private static void ValidarNombreTabla(string tabla)
    {
        if (!NombreTablaValido().IsMatch(tabla))
            throw new ArgumentException($"Nombre de dimensión no permitido: '{tabla}'. Debe tener la forma dbo.Dim_Nombre.");
    }

    private static string? Recortar(string? texto, int maximo) =>
        texto is null || texto.Length <= maximo ? texto : texto[..maximo];

    [GeneratedRegex(@"^dbo\.Dim_[A-Za-z]{1,100}$")]
    private static partial Regex NombreTablaValido();
}
