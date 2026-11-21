using OpinionesClientes.Application.Models;

namespace OpinionesClientes.Application.Interfaces;

/// <summary>
/// Destino de los datos extraídos (tablas staging o archivos temporales).
/// Los extractores no saben dónde se guardan los datos: solo usan este contrato.
/// </summary>
public interface IStagingRepository
{
    /// <summary>Guarda los registros en la tabla staging indicada y devuelve cuántos se guardaron.</summary>
    Task<int> GuardarAsync<TClass>(string tablaStaging, IReadOnlyCollection<TClass> registros,
        ContextoLote contexto, CancellationToken cancellationToken) where TClass : class;

    /// <summary>Registra el resultado de la extracción de una fuente (bitácora).</summary>
    Task RegistrarResultadoAsync(ResultadoExtraccion resultado, CancellationToken cancellationToken);
}
