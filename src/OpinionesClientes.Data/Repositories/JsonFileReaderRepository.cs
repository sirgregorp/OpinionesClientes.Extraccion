using System.Text.Json;
using OpinionesClientes.Data.Interfaces;

namespace OpinionesClientes.Data.Repositories;

/// <summary>
/// Lee archivos JSON (un arreglo de objetos). Muestra cómo la fábrica puede
/// incorporar un formato nuevo sin cambiar a quien consume IFileReaderRepository.
/// </summary>
public class JsonFileReaderRepository<TClass> : IFileReaderRepository<TClass> where TClass : class
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public async Task<IEnumerable<TClass>> ReadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"No se encontró el archivo JSON '{filePath}'.", filePath);

        await using var flujo = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<TClass>>(flujo, Opciones, cancellationToken) ?? new List<TClass>();
    }
}
