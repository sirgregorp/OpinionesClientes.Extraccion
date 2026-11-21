namespace OpinionesClientes.Data.Interfaces;

/// <summary>
/// Contrato genérico para leer archivos (CSV, JSON...).
/// La lógica de negocio depende de esta interfaz y no del formato físico del archivo.
/// </summary>
/// <typeparam name="TClass">Entidad a la que se mapea cada registro.</typeparam>
public interface IFileReaderRepository<TClass> where TClass : class
{
    Task<IEnumerable<TClass>> ReadFileAsync(string filePath, CancellationToken cancellationToken = default);
}
