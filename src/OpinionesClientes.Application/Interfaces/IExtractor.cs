using OpinionesClientes.Application.Models;
using OpinionesClientes.Application.Options;

namespace OpinionesClientes.Application.Interfaces;

/// <summary>
/// Contrato común de todos los extractores (CsvExtractor, DatabaseExtractor, ApiExtractor).
/// El orquestador solo conoce esta interfaz, no el formato físico de cada fuente.
/// </summary>
public interface IExtractor
{
    FuenteOptions Fuente { get; }

    /// <summary>Extrae los datos de la fuente y los deja en el área de staging.</summary>
    Task<ResultadoExtraccion> ExtraerAsync(Guid idLote, CancellationToken cancellationToken);
}
