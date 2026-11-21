using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Models;
using OpinionesClientes.Application.Options;

namespace OpinionesClientes.Data.Extractors;

/// <summary>Registros leídos de una fuente y cuántos se descartaron durante la lectura.</summary>
public record LecturaFuente<TClass>(IReadOnlyCollection<TClass> Registros, int Rechazados);

/// <summary>
/// Plantilla común de todos los extractores (patrón Template Method):
/// medir tiempo → leer la fuente → validar claves → guardar en staging → registrar en el log.
/// Cada extractor concreto solo implementa cómo se lee su fuente.
/// </summary>
public abstract class ExtractorBase<TClass> : IExtractor where TClass : class
{
    private readonly IStagingRepository _staging;
    protected ILogger Logger { get; }

    public FuenteOptions Fuente { get; }
    protected abstract string TipoFuente { get; }

    protected ExtractorBase(FuenteOptions fuente, IStagingRepository staging, ILogger logger)
    {
        Fuente = fuente;
        _staging = staging;
        Logger = logger;
    }

    /// <summary>Descripción del origen para el log (ruta, base de datos o URL).</summary>
    protected abstract string DescribirOrigen();

    /// <summary>Lectura propia de cada tipo de fuente.</summary>
    protected abstract Task<LecturaFuente<TClass>> LeerAsync(CancellationToken cancellationToken);

    public async Task<ResultadoExtraccion> ExtraerAsync(Guid idLote, CancellationToken cancellationToken)
    {
        var origen = DescribirOrigen();
        var resultado = new ResultadoExtraccion
        {
            IdLote = idLote,
            Fuente = Fuente.Nombre,
            TipoFuente = TipoFuente,
            Entidad = typeof(TClass).Name,
            TablaStaging = Fuente.TablaStaging,
            Origen = origen,
            FechaInicio = DateTime.Now
        };

        // RENDIMIENTO: cada fuente mide su propio tiempo con Stopwatch
        var reloj = Stopwatch.StartNew();
        Logger.LogInformation("[{Fuente}] Inicio de extracción ({Tipo}) desde {Origen}", Fuente.Nombre, TipoFuente, origen);

        try
        {
            var lectura = await LeerAsync(cancellationToken);
            var validos = DescartarSinClave(lectura.Registros, out var sinClave);

            resultado.RegistrosExtraidos = validos.Count;
            resultado.RegistrosRechazados = lectura.Rechazados + sinClave;

            var contexto = new ContextoLote(idLote, DateTime.Now, Fuente.Nombre, origen);
            resultado.RegistrosGuardados = await _staging.GuardarAsync(Fuente.TablaStaging, validos, contexto, cancellationToken);
            resultado.Exitoso = true;

            Logger.LogInformation(
                "[{Fuente}] {Extraidos} registros extraídos, {Rechazados} rechazados, {Guardados} guardados en {Tabla} ({Duracion} ms)",
                Fuente.Nombre, resultado.RegistrosExtraidos, resultado.RegistrosRechazados,
                resultado.RegistrosGuardados, Fuente.TablaStaging, reloj.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // el servicio se está deteniendo
        }
        catch (Exception ex)
        {
            // Una fuente con error no detiene a las demás: se registra y se continúa
            resultado.Exitoso = false;
            resultado.MensajeError = ex.Message;
            Logger.LogError("[{Fuente}] Error en la extracción ({Tipo}): {Mensaje}", Fuente.Nombre, ex.GetType().Name, ex.Message);
            Logger.LogDebug(ex, "[{Fuente}] Detalle técnico del error", Fuente.Nombre);
        }
        finally
        {
            reloj.Stop();
            resultado.FechaFin = DateTime.Now;
            resultado.DuracionMs = reloj.ElapsedMilliseconds;
        }

        return resultado;
    }

    /// <summary>
    /// Validación mínima de la extracción: un registro sin identificador (la primera
    /// propiedad de la entidad, por ejemplo IdOpinion) no se puede rastrear y se descarta.
    /// Las demás reglas de calidad se aplican en la Transformación.
    /// </summary>
    private IReadOnlyCollection<TClass> DescartarSinClave(IReadOnlyCollection<TClass> registros, out int descartados)
    {
        var clave = typeof(TClass).GetProperties(BindingFlags.Public | BindingFlags.Instance).First();
        var validos = registros.Where(r => !string.IsNullOrWhiteSpace(clave.GetValue(r)?.ToString())).ToList();
        descartados = registros.Count - validos.Count;

        if (descartados > 0)
            Logger.LogWarning("[{Fuente}] {Cantidad} registros descartados por no tener {Clave}",
                Fuente.Nombre, descartados, clave.Name);

        return validos;
    }
}
