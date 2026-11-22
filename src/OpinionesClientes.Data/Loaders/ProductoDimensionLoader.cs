using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Application.Transformaciones;
using OpinionesClientes.Data.Interfaces;
using OpinionesClientes.Domain.Entities;
using OpinionesClientes.Domain.Entities.Dimensiones;

namespace OpinionesClientes.Data.Loaders;

/// <summary>Dim_Producto: productos de stg.Productos.</summary>
public class ProductoDimensionLoader : DimensionLoaderBase<DimProducto>
{
    public ProductoDimensionLoader(IFileFactory fileFactory, IDataWarehouseRepository dataWarehouse,
        IOptions<CargaDimensionesOptions> opciones, ILogger<ProductoDimensionLoader> logger)
        : base(fileFactory, dataWarehouse, opciones, logger) { }

    public override string Dimension => "Producto";
    protected override string TablaDestino => "dbo.Dim_Producto";
    protected override string TablaOrigen => "stg.Productos";
    protected override IReadOnlyCollection<string> ColumnasLlave { get; } = new[] { nameof(DimProducto.IdProductoOrigen) };

    protected override async Task<DatosDimension<DimProducto>> PrepararAsync(CancellationToken cancellationToken)
    {
        var productos = await LeerStagingAsync<Producto>(
            "SELECT IdProducto, Nombre, Categoria FROM stg.Productos", cancellationToken);

        var (filas, descartados) = ReglasDimensiones.TransformarProductos(productos);
        return new DatosDimension<DimProducto>(filas, productos.Count, descartados);
    }
}
