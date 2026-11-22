using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Models;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Data.Interfaces;

namespace OpinionesClientes.Data.Loaders;

/// <summary>Filas listas para la dimensión, cuántas se leyeron de staging y cuántas se descartaron.</summary>
public record DatosDimension<TDim>(IReadOnlyCollection<TDim> Filas, int Leidos, int Descartados);

/// <summary>
/// Plantilla común de los cargadores de dimensiones (Template Method), igual que ExtractorBase:
/// medir tiempo → leer staging → transformar → MERGE en el DW → armar el resultado.
/// Cada dimensión solo implementa qué lee y cómo lo transforma.
/// </summary>
public abstract class DimensionLoaderBase<TDim> : IDimensionLoader where TDim : class
{
    private readonly IFileFactory _fileFactory;
    private readonly IDataWarehouseRepository _dataWarehouse;
    protected CargaDimensionesOptions Opciones { get; }
    protected ILogger Logger { get; }

    public abstract string Dimension { get; }
    protected abstract string TablaDestino { get; }
    protected abstract string TablaOrigen { get; }

    /// <summary>Llave de negocio con la que se compara contra la dimensión.</summary>
    protected abstract IReadOnlyCollection<string> ColumnasLlave { get; }

    protected DimensionLoaderBase(IFileFactory fileFactory, IDataWarehouseRepository dataWarehouse,
        IOptions<CargaDimensionesOptions> opciones, ILogger logger)
    {
        _fileFactory = fileFactory;
        _dataWarehouse = dataWarehouse;
        Opciones = opciones.Value;
        Logger = logger;
    }

    /// <summary>Lee staging y aplica las reglas de la dimensión.</summary>
    protected abstract Task<DatosDimension<TDim>> PrepararAsync(CancellationToken cancellationToken);

    /// <summary>Lectura de staging con el mismo lector de base de datos que usa la extracción.</summary>
    protected async Task<List<TStg>> LeerStagingAsync<TStg>(string consulta, CancellationToken cancellationToken)
        where TStg : class
    {
        var lector = _fileFactory.CreateDatabaseReader<TStg>();
        var registros = await lector.ReadDataAsync(Opciones.ConexionStaging, consulta, cancellationToken);
        return registros.ToList();
    }

    public async Task<ResultadoCargaDimension> CargarAsync(Guid idLote, CancellationToken cancellationToken)
    {
        var resultado = new ResultadoCargaDimension
        {
            IdLote = idLote,
            Dimension = Dimension,
            TablaDestino = TablaDestino,
            TablaOrigen = TablaOrigen,
            FechaInicio = DateTime.Now
        };

        var reloj = Stopwatch.StartNew();
        Logger.LogInformation("[{Dimension}] Inicio de carga desde {Origen}", Dimension, TablaOrigen);

        try
        {
            var datos = await PrepararAsync(cancellationToken);
            resultado.RegistrosLeidos = datos.Leidos;
            resultado.RegistrosDescartados = datos.Descartados;

            if (datos.Descartados > 0)
                Logger.LogWarning("[{Dimension}] {Cantidad} registros descartados por las reglas de limpieza",
                    Dimension, datos.Descartados);

            var merge = await _dataWarehouse.GuardarDimensionAsync(TablaDestino, datos.Filas, ColumnasLlave, cancellationToken);
            resultado.Insertados = merge.Insertados;
            resultado.Actualizados = merge.Actualizados;
            resultado.SinCambios = merge.SinCambios;
            resultado.TotalEnDimension = merge.TotalEnDimension;
            resultado.Exitoso = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            resultado.Exitoso = false;
            resultado.MensajeError = ex.Message;
            Logger.LogError(ex, "[{Dimension}] Error en la carga", Dimension);
        }
        finally
        {
            reloj.Stop();
            resultado.DuracionMs = reloj.ElapsedMilliseconds;
            resultado.FechaFin = DateTime.Now;
        }

        if (resultado.Exitoso)
            Logger.LogInformation(
                "[{Dimension}] Fin: {Leidos} leídos, {Descartados} descartados, {Ins} insertados, {Act} actualizados, " +
                "{Igual} sin cambios. Total en {Tabla}: {Total}. {Ms} ms",
                Dimension, resultado.RegistrosLeidos, resultado.RegistrosDescartados, resultado.Insertados,
                resultado.Actualizados, resultado.SinCambios, TablaDestino, resultado.TotalEnDimension, resultado.DuracionMs);

        return resultado;
    }
}
