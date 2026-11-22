using OpinionesClientes.Application.Models;

namespace OpinionesClientes.Application.Interfaces;

/// <summary>Escritura en el Data Warehouse (OpinionesDW).</summary>
public interface IDataWarehouseRepository
{
    /// <summary>
    /// Inserta las filas nuevas y actualiza las que cambiaron (MERGE, SCD tipo 1).
    /// Las filas se comparan por las columnas llave (llave de negocio).
    /// </summary>
    Task<ResultadoMerge> GuardarDimensionAsync<TDim>(string tablaDimension, IReadOnlyCollection<TDim> filas,
        IReadOnlyCollection<string> columnasLlave, CancellationToken cancellationToken) where TDim : class;

    Task RegistrarResultadoAsync(ResultadoCargaDimension resultado, CancellationToken cancellationToken);
}
