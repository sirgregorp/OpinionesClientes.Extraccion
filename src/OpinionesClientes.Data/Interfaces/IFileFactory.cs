namespace OpinionesClientes.Data.Interfaces;

/// <summary>
/// Fábrica de lectores (patrón Factory). Decide qué implementación concreta
/// usar según el origen de los datos: extensión del archivo, base de datos o API.
/// </summary>
public interface IFileFactory
{
    IFileReaderRepository<TClass> CreateFileReader<TClass>(string filePath) where TClass : class;
    IDatabaseReaderRepository<TClass> CreateDatabaseReader<TClass>() where TClass : class;
    IApiReaderRepository<TClass> CreateApiReader<TClass>() where TClass : class;
}
