using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Models;
using OpinionesClientes.Data.Utils;

namespace OpinionesClientes.Data.Repositories;

/// <summary>
/// Guarda los datos extraídos en las tablas staging (esquema stg) de OpinionesStagingDB
/// usando SqlBulkCopy, que inserta miles de filas en una sola operación.
/// </summary>
public partial class SqlStagingRepository : IStagingRepository
{
    private readonly string _cadenaConexion;
    private readonly StagingOptions _opciones;
    private readonly ILogger<SqlStagingRepository> _logger;

    public SqlStagingRepository(IConfiguration configuracion, IOptions<StagingOptions> opciones,
        ILogger<SqlStagingRepository> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
        _cadenaConexion = configuracion.GetConnectionString(_opciones.Conexion)
            ?? throw new InvalidOperationException($"No existe la cadena de conexión '{_opciones.Conexion}'.");
    }

    public async Task<int> GuardarAsync<TClass>(string tablaStaging, IReadOnlyCollection<TClass> registros,
        ContextoLote contexto, CancellationToken cancellationToken) where TClass : class
    {
        ValidarNombreTabla(tablaStaging);

        using var datos = ObjectMapper.ADataTable(registros, contexto);

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);

        // Todo en una transacción: o se cargan todos los registros de la fuente o ninguno
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        if (_opciones.LimpiarAntesDeCargar)
        {
            await using var limpiar = new SqlCommand($"TRUNCATE TABLE {tablaStaging};", conexion, transaccion);
            await limpiar.ExecuteNonQueryAsync(cancellationToken);
        }

        using (var bulk = new SqlBulkCopy(conexion, SqlBulkCopyOptions.CheckConstraints, transaccion))
        {
            bulk.DestinationTableName = tablaStaging;
            bulk.BatchSize = _opciones.TamanoLoteBulk;
            bulk.BulkCopyTimeout = _opciones.TimeoutSegundos;

            foreach (DataColumn columna in datos.Columns)
                bulk.ColumnMappings.Add(columna.ColumnName, columna.ColumnName);

            await bulk.WriteToServerAsync(datos, cancellationToken);
        }

        await transaccion.CommitAsync(cancellationToken);

        _logger.LogDebug("{Cantidad} filas insertadas en {Tabla}", datos.Rows.Count, tablaStaging);
        return datos.Rows.Count;
    }

    public async Task RegistrarResultadoAsync(ResultadoExtraccion r, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO stg.LogExtraccion
                (IdLote, Fuente, TipoFuente, Entidad, TablaStaging, Origen,
                 RegistrosExtraidos, RegistrosRechazados, RegistrosGuardados,
                 FechaInicio, FechaFin, DuracionMs, Estado, MensajeError)
            VALUES
                (@IdLote, @Fuente, @TipoFuente, @Entidad, @TablaStaging, @Origen,
                 @Extraidos, @Rechazados, @Guardados,
                 @Inicio, @Fin, @Duracion, @Estado, @Error);
            """;

        await using var conexion = new SqlConnection(_cadenaConexion);
        await conexion.OpenAsync(cancellationToken);
        await using var comando = new SqlCommand(sql, conexion);

        // SEGURIDAD: siempre parámetros, nunca concatenación de valores en el SQL
        comando.Parameters.Add("@IdLote", SqlDbType.UniqueIdentifier).Value = r.IdLote;
        comando.Parameters.Add("@Fuente", SqlDbType.NVarChar, 100).Value = r.Fuente;
        comando.Parameters.Add("@TipoFuente", SqlDbType.NVarChar, 20).Value = r.TipoFuente;
        comando.Parameters.Add("@Entidad", SqlDbType.NVarChar, 50).Value = r.Entidad;
        comando.Parameters.Add("@TablaStaging", SqlDbType.NVarChar, 128).Value = r.TablaStaging;
        comando.Parameters.Add("@Origen", SqlDbType.NVarChar, 300).Value = Recortar(r.Origen, 300);
        comando.Parameters.Add("@Extraidos", SqlDbType.Int).Value = r.RegistrosExtraidos;
        comando.Parameters.Add("@Rechazados", SqlDbType.Int).Value = r.RegistrosRechazados;
        comando.Parameters.Add("@Guardados", SqlDbType.Int).Value = r.RegistrosGuardados;
        comando.Parameters.Add("@Inicio", SqlDbType.DateTime2).Value = r.FechaInicio;
        comando.Parameters.Add("@Fin", SqlDbType.DateTime2).Value = r.FechaFin;
        comando.Parameters.Add("@Duracion", SqlDbType.BigInt).Value = r.DuracionMs;
        comando.Parameters.Add("@Estado", SqlDbType.NVarChar, 10).Value = r.Estado;
        comando.Parameters.Add("@Error", SqlDbType.NVarChar, 1000).Value =
            (object?)Recortar(r.MensajeError, 1000) ?? DBNull.Value;

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// SEGURIDAD: el nombre de la tabla no se puede pasar como parámetro SQL,
    /// así que solo se aceptan nombres del esquema stg con letras y guion bajo.
    /// </summary>
    private static void ValidarNombreTabla(string tabla)
    {
        if (!NombreTablaValido().IsMatch(tabla))
            throw new ArgumentException($"Nombre de tabla staging no permitido: '{tabla}'. Debe tener la forma stg.Nombre.");
    }

    private static string? Recortar(string? texto, int maximo) =>
        texto is null || texto.Length <= maximo ? texto : texto[..maximo];

    [GeneratedRegex(@"^stg\.[A-Za-z_]{1,100}$")]
    private static partial Regex NombreTablaValido();
}
