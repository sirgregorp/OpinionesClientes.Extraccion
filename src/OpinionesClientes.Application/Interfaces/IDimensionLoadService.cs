using OpinionesClientes.Application.Models;

namespace OpinionesClientes.Application.Interfaces;

/// <summary>Orquesta la carga de todas las dimensiones.</summary>
public interface IDimensionLoadService
{
    Task<ResumenCargaDimensiones> EjecutarAsync(CancellationToken cancellationToken);
}
