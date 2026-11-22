using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Models;
using OpinionesClientes.Application.Options;

namespace OpinionesClientes.Application.Services;

/// <summary>
/// Orquestador de la carga de dimensiones del Data Warehouse.
/// Las dimensiones no dependen unas de otras, así que se cargan en paralelo
/// (con un límite), igual que las fuentes en la extracción.
/// </summary>
public class DimensionLoadService : IDimensionLoadService
{
    private readonly IEnumerable<IDimensionLoader> _cargadores;
    private readonly IDataWarehouseRepository _dataWarehouse;
    private readonly CargaDimensionesOptions _opciones;
    private readonly ILogger<DimensionLoadService> _logger;

    public DimensionLoadService(IEnumerable<IDimensionLoader> cargadores, IDataWarehouseRepository dataWarehouse,
        IOptions<CargaDimensionesOptions> opciones, ILogger<DimensionLoadService> logger)
    {
        _cargadores = cargadores;
        _dataWarehouse = dataWarehouse;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task<ResumenCargaDimensiones> EjecutarAsync(CancellationToken cancellationToken)
    {
        var resumen = new ResumenCargaDimensiones { IdLote = Guid.NewGuid(), Inicio = DateTime.Now };
        var reloj = Stopwatch.StartNew();
        var cargadores = _cargadores.ToList();

        _logger.LogInformation("Inicio de la carga de dimensiones {IdLote}: {Cantidad} dimensiones",
            resumen.IdLote, cargadores.Count);

        using var semaforo = new SemaphoreSlim(Math.Max(1, _opciones.MaxParalelismo));

        var tareas = cargadores.Select(async cargador =>
        {
            await semaforo.WaitAsync(cancellationToken);
            try
            {
                return await cargador.CargarAsync(resumen.IdLote, cancellationToken);
            }
            finally
            {
                semaforo.Release();
            }
        });

        var resultados = await Task.WhenAll(tareas);
        resumen.Resultados.AddRange(resultados.OrderBy(r => r.Dimension));

        foreach (var resultado in resumen.Resultados)
        {
            try
            {
                await _dataWarehouse.RegistrarResultadoAsync(resultado, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar en la bitácora el resultado de {Dimension}", resultado.Dimension);
            }
        }

        reloj.Stop();
        resumen.DuracionTotalMs = reloj.ElapsedMilliseconds;

        _logger.LogInformation(
            "Fin de la carga de dimensiones {IdLote}: {Insertados} insertados, {Actualizados} actualizados, " +
            "{SinCambios} sin cambios, {Descartados} descartados, {Errores} con error. Duración {Total} ms",
            resumen.IdLote, resumen.TotalInsertados, resumen.TotalActualizados, resumen.TotalSinCambios,
            resumen.TotalDescartados, resumen.DimensionesConError, resumen.DuracionTotalMs);

        return resumen;
    }
}
