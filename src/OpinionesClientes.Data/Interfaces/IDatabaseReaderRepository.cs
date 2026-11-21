namespace OpinionesClientes.Data.Interfaces;

/// <summary>Contrato genérico para leer registros desde una base de datos relacional.</summary>
public interface IDatabaseReaderRepository<TClass> where TClass : class
{
    /// <param name="connectionName">Nombre de la cadena de conexión en appsettings.json (nunca la cadena en sí).</param>
    /// <param name="query">Consulta SELECT que se ejecuta en el origen.</param>
    Task<IEnumerable<TClass>> ReadDataAsync(string connectionName, string query, CancellationToken cancellationToken = default);
}
