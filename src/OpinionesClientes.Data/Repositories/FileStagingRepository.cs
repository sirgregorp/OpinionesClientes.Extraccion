using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Models;

namespace OpinionesClientes.Data.Repositories;

/// <summary>
/// Alternativa a las tablas staging: guarda los datos extraídos en archivos JSON temporales
/// (una carpeta por lote y un archivo por fuente). Útil cuando no hay SQL Server disponible.
/// </summary>
public class FileStagingRepository : IStagingRepository
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // conserva tildes y ñ legibles
    };

    private readonly StagingOptions _opciones;
    private readonly ILogger<FileStagingRepository> _logger;

    public FileStagingRepository(IOptions<StagingOptions> opciones, ILogger<FileStagingRepository> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
    }

    private string CarpetaBase => Path.IsPathRooted(_opciones.CarpetaArchivos)
        ? _opciones.CarpetaArchivos
        : Path.Combine(AppContext.BaseDirectory, _opciones.CarpetaArchivos);

    public async Task<int> GuardarAsync<TClass>(string tablaStaging, IReadOnlyCollection<TClass> registros,
        ContextoLote contexto, CancellationToken cancellationToken) where TClass : class
    {
        var carpetaLote = Path.Combine(CarpetaBase, $"lote_{contexto.IdLote:N}");
        Directory.CreateDirectory(carpetaLote);

        var archivo = Path.Combine(carpetaLote, $"{tablaStaging}.json");
        var contenido = new
        {
            contexto.IdLote,
            contexto.FechaExtraccion,
            contexto.Fuente,
            contexto.Origen,
            Cantidad = registros.Count,
            Registros = registros
        };

        await using var flujo = File.Create(archivo);
        await JsonSerializer.SerializeAsync(flujo, contenido, Opciones, cancellationToken);

        _logger.LogDebug("{Cantidad} registros guardados en {Archivo}", registros.Count, archivo);
        return registros.Count;
    }

    public async Task RegistrarResultadoAsync(ResultadoExtraccion resultado, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(CarpetaBase);
        var linea = JsonSerializer.Serialize(resultado, new JsonSerializerOptions { Encoder = Opciones.Encoder });
        await File.AppendAllTextAsync(Path.Combine(CarpetaBase, "log_extraccion.jsonl"),
            linea + Environment.NewLine, cancellationToken);
    }
}
