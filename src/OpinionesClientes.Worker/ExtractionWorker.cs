using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;

namespace OpinionesClientes.Worker;

/// <summary>
/// Servicio en segundo plano (Worker Service) que ejecuta el proceso ETL:
/// 1) extracción de las fuentes a staging y 2) carga de las dimensiones del Data Warehouse.
/// Con IntervaloMinutos = 0 corre una vez y termina; con un valor mayor se repite periódicamente.
/// </summary>
public class ExtractionWorker : BackgroundService
{
    private readonly IExtractionService _servicio;
    private readonly IDimensionLoadService _dimensiones;
    private readonly IHostApplicationLifetime _cicloVida;
    private readonly ExtraccionOptions _opciones;
    private readonly CargaDimensionesOptions _opcionesDimensiones;
    private readonly ILogger<ExtractionWorker> _logger;

    public ExtractionWorker(IExtractionService servicio, IDimensionLoadService dimensiones,
        IHostApplicationLifetime cicloVida, IOptions<ExtraccionOptions> opciones,
        IOptions<CargaDimensionesOptions> opcionesDimensiones, ILogger<ExtractionWorker> logger)
    {
        _servicio = servicio;
        _dimensiones = dimensiones;
        _cicloVida = cicloVida;
        _opciones = opciones.Value;
        _opcionesDimensiones = opcionesDimensiones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker ETL iniciado ({Fuentes} fuentes configuradas, carga de dimensiones {Estado})",
            _opciones.Fuentes.Count, _opcionesDimensiones.Habilitada ? "habilitada" : "deshabilitada");

        try
        {
            do
            {
                var extraccionCorrecta = true;

                // Paso 1: extracción (fuentes → staging)
                if (_opciones.Habilitada)
                {
                    try
                    {
                        var resumen = await _servicio.EjecutarAsync(stoppingToken);
                        ReporteConsola.Mostrar(resumen);
                        extraccionCorrecta = resumen.FuentesConError == 0;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        extraccionCorrecta = false;
                        _logger.LogCritical(ex, "Falla general del proceso de extracción");
                    }
                }
                else
                {
                    _logger.LogInformation("Extracción deshabilitada: se usan los datos que ya están en staging");
                }

                // Paso 2: carga de dimensiones (staging → OpinionesDW)
                if (_opcionesDimensiones.Habilitada)
                {
                    if (!extraccionCorrecta)
                        _logger.LogWarning("La extracción tuvo errores: las dimensiones se cargan con lo que hay en staging");

                    try
                    {
                        var resumenDimensiones = await _dimensiones.EjecutarAsync(stoppingToken);
                        ReporteConsola.MostrarDimensiones(resumenDimensiones);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogCritical(ex, "Falla general de la carga de dimensiones");
                    }
                }

                if (_opciones.IntervaloMinutos <= 0) break;

                _logger.LogInformation("Próxima ejecución en {Minutos} minutos", _opciones.IntervaloMinutos);
                await Task.Delay(TimeSpan.FromMinutes(_opciones.IntervaloMinutos), stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Proceso cancelado: el servicio se está deteniendo");
        }

        _logger.LogInformation("Worker ETL finalizado");
        _cicloVida.StopApplication();
    }
}
