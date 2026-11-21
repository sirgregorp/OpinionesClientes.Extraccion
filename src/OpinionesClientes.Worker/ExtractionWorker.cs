using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;

namespace OpinionesClientes.Worker;

/// <summary>
/// Servicio en segundo plano (Worker Service) que ejecuta el proceso de extracción.
/// Con IntervaloMinutos = 0 corre una vez y termina; con un valor mayor se repite periódicamente.
/// </summary>
public class ExtractionWorker : BackgroundService
{
    private readonly IExtractionService _servicio;
    private readonly IHostApplicationLifetime _cicloVida;
    private readonly ExtraccionOptions _opciones;
    private readonly ILogger<ExtractionWorker> _logger;

    public ExtractionWorker(IExtractionService servicio, IHostApplicationLifetime cicloVida,
        IOptions<ExtraccionOptions> opciones, ILogger<ExtractionWorker> logger)
    {
        _servicio = servicio;
        _cicloVida = cicloVida;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de extracción iniciado ({Fuentes} fuentes configuradas)", _opciones.Fuentes.Count);

        try
        {
            do
            {
                try
                {
                    var resumen = await _servicio.EjecutarAsync(stoppingToken);
                    ReporteConsola.Mostrar(resumen);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogCritical(ex, "Falla general del proceso de extracción");
                }

                if (_opciones.IntervaloMinutos <= 0) break;

                _logger.LogInformation("Próxima extracción en {Minutos} minutos", _opciones.IntervaloMinutos);
                await Task.Delay(TimeSpan.FromMinutes(_opciones.IntervaloMinutos), stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Extracción cancelada: el servicio se está deteniendo");
        }

        _logger.LogInformation("Worker de extracción finalizado");
        _cicloVida.StopApplication();
    }
}
