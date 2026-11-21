using OpinionesClientes.Application.Models;

namespace OpinionesClientes.Application.Interfaces;

/// <summary>Orquesta la extracción de todas las fuentes configuradas.</summary>
public interface IExtractionService
{
    Task<ResumenExtraccion> EjecutarAsync(CancellationToken cancellationToken);
}
