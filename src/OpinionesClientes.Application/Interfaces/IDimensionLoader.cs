using OpinionesClientes.Application.Models;

namespace OpinionesClientes.Application.Interfaces;

/// <summary>Carga una dimensión del Data Warehouse a partir de staging.</summary>
public interface IDimensionLoader
{
    string Dimension { get; }
    Task<ResultadoCargaDimension> CargarAsync(Guid idLote, CancellationToken cancellationToken);
}
