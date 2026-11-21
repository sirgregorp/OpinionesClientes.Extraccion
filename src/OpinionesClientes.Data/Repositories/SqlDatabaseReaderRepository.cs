using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpinionesClientes.Data.Interfaces;
using OpinionesClientes.Data.Utils;

namespace OpinionesClientes.Data.Repositories;

/// <summary>
/// Lee registros de SQL Server con ADO.NET (Microsoft.Data.SqlClient) de forma asíncrona.
/// La cadena de conexión se obtiene por nombre desde la configuración.
/// </summary>
public class SqlDatabaseReaderRepository<TClass> : IDatabaseReaderRepository<TClass>, IRegistraRechazos
    where TClass : class
{
    private readonly IConfiguration _configuracion;
    private readonly ILogger<SqlDatabaseReaderRepository<TClass>> _logger;

    public int RegistrosRechazados { get; private set; }

    public SqlDatabaseReaderRepository(IConfiguration configuracion, ILogger<SqlDatabaseReaderRepository<TClass>> logger)
    {
        _configuracion = configuracion;
        _logger = logger;
    }

    public async Task<IEnumerable<TClass>> ReadDataAsync(string connectionName, string query, CancellationToken cancellationToken = default)
    {
        var cadena = _configuracion.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException($"No existe la cadena de conexión '{connectionName}' en la configuración.");

        RegistrosRechazados = 0;
        var registros = new List<TClass>();

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancellationToken);

        // Solo lectura: la consulta viene de la configuración, no de datos del usuario
        await using var comando = new SqlCommand(query, conexion) { CommandType = CommandType.Text, CommandTimeout = 120 };

        // SequentialAccess: lee las columnas en orden, con menos memoria para resultados grandes
        await using var lector = await comando.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);

        var mapa = ObjectMapper.MapaColumnas<TClass>(lector);
        var fila = 0;

        while (await lector.ReadAsync(cancellationToken))
        {
            fila++;
            try
            {
                registros.Add(ObjectMapper.CrearDesde<TClass>(lector, mapa));
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                RegistrosRechazados++;
                _logger.LogWarning("{Conexion}: fila {Fila} rechazada. {Motivo}", connectionName, fila, ex.Message);
            }
        }

        return registros;
    }
}
