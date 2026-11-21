using Microsoft.Extensions.DependencyInjection;
using OpinionesClientes.Data.Interfaces;
using OpinionesClientes.Data.Repositories;

namespace OpinionesClientes.Data.Factory;

/// <summary>
/// Patrón Factory: crea dinámicamente el lector correspondiente al origen de datos.
/// Quien la usa pide "un lector de Encuesta para este archivo" y la fábrica decide
/// la clase concreta. Para soportar Excel, por ejemplo, solo se agrega un caso ".xlsx".
/// </summary>
public class FileFactory : IFileFactory
{
    private readonly IServiceProvider _serviceProvider;

    public FileFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IFileReaderRepository<TClass> CreateFileReader<TClass>(string filePath) where TClass : class
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".csv" => ActivatorUtilities.CreateInstance<CsvFileReaderRepository<TClass>>(_serviceProvider),
            ".json" => ActivatorUtilities.CreateInstance<JsonFileReaderRepository<TClass>>(_serviceProvider),
            _ => throw new NotSupportedException($"No hay un lector para archivos '{extension}' ({filePath}).")
        };
    }

    public IDatabaseReaderRepository<TClass> CreateDatabaseReader<TClass>() where TClass : class =>
        ActivatorUtilities.CreateInstance<SqlDatabaseReaderRepository<TClass>>(_serviceProvider);

    public IApiReaderRepository<TClass> CreateApiReader<TClass>() where TClass : class =>
        ActivatorUtilities.CreateInstance<ApiReaderRepository<TClass>>(_serviceProvider);
}
