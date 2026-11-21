namespace OpinionesClientes.Data.Interfaces;

/// <summary>Contrato genérico para consumir una API REST paginada.</summary>
public interface IApiReaderRepository<TClass> where TClass : class
{
    Task<IEnumerable<TClass>> ReadApiAsync(string endpoint, CancellationToken cancellationToken = default);
}
