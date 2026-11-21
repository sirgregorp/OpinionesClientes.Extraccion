using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Models;
using OpinionesClientes.Application.Options;

namespace OpinionesClientes.Application.Services;

/// <summary>
/// Orquestador del proceso de extracción (la "E" del ETL).
/// Ejecuta los extractores en paralelo, con un límite de concurrencia,
/// y mide el tiempo total con Stopwatch.
/// </summary>
public class ExtractionService : IExtractionService
{
    private readonly IExtractorFactory _factory;
    private readonly IStagingRepository _staging;
    private readonly ExtraccionOptions _opciones;
    private readonly ILogger<ExtractionService> _logger;

    public ExtractionService(IExtractorFactory factory, IStagingRepository staging,
        IOptions<ExtraccionOptions> opciones, ILogger<ExtractionService> logger)
    {
        _factory = factory;
        _staging = staging;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task<ResumenExtraccion> EjecutarAsync(CancellationToken cancellationToken)
    {
        var resumen = new ResumenExtraccion { IdLote = Guid.NewGuid(), Inicio = DateTime.Now };
        var reloj = Stopwatch.StartNew();

        var extractores = _factory.CrearExtractores();
        var maxParalelismo = Math.Max(1, _opciones.MaxParalelismo);

        _logger.LogInformation("Inicio del lote {IdLote}: {Cantidad} fuentes, paralelismo máximo {Max}",
            resumen.IdLote, extractores.Count, maxParalelismo);

        // RENDIMIENTO: las fuentes son independientes entre sí, así que se extraen en paralelo.
        // El semáforo limita cuántas corren a la vez para no saturar la BD ni la API.
        using var semaforo = new SemaphoreSlim(maxParalelismo);

        var tareas = extractores.Select(async extractor =>
        {
            await semaforo.WaitAsync(cancellationToken);
            try
            {
                return await extractor.ExtraerAsync(resumen.IdLote, cancellationToken);
            }
            finally
            {
                semaforo.Release();
            }
        });

        var resultados = await Task.WhenAll(tareas);
        resumen.Resultados.AddRange(resultados);

        // Bitácora en staging (stg.LogExtraccion o archivo): un registro por fuente
        foreach (var resultado in resultados)
        {
            try
            {
                await _staging.RegistrarResultadoAsync(resultado, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar en la bitácora el resultado de {Fuente}", resultado.Fuente);
            }
        }

        reloj.Stop();
        resumen.DuracionTotalMs = reloj.ElapsedMilliseconds;

        _logger.LogInformation(
            "Fin del lote {IdLote}: {Extraidos} extraídos, {Guardados} guardados en staging, {Rechazados} rechazados, " +
            "{Errores} fuentes con error. Duración total {Total} ms (suma secuencial {Suma} ms)",
            resumen.IdLote, resumen.TotalExtraidos, resumen.TotalGuardados, resumen.TotalRechazados,
            resumen.FuentesConError, resumen.DuracionTotalMs, resumen.SumaDuracionesMs);

        return resumen;
    }
}
