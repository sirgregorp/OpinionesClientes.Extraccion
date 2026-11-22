using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Application.Transformaciones;
using OpinionesClientes.Data.Interfaces;
using OpinionesClientes.Domain.Entities;
using OpinionesClientes.Domain.Entities.Dimensiones;

namespace OpinionesClientes.Data.Loaders;

/// <summary>Dim_Cliente: clientes de stg.Clientes más el miembro "Anónimo".</summary>
public class ClienteDimensionLoader : DimensionLoaderBase<DimCliente>
{
    public ClienteDimensionLoader(IFileFactory fileFactory, IDataWarehouseRepository dataWarehouse,
        IOptions<CargaDimensionesOptions> opciones, ILogger<ClienteDimensionLoader> logger)
        : base(fileFactory, dataWarehouse, opciones, logger) { }

    public override string Dimension => "Cliente";
    protected override string TablaDestino => "dbo.Dim_Cliente";
    protected override string TablaOrigen => "stg.Clientes";
    protected override IReadOnlyCollection<string> ColumnasLlave { get; } = new[] { nameof(DimCliente.IdClienteOrigen) };

    protected override async Task<DatosDimension<DimCliente>> PrepararAsync(CancellationToken cancellationToken)
    {
        var clientes = await LeerStagingAsync<Cliente>(
            "SELECT IdCliente, Nombre, Email FROM stg.Clientes", cancellationToken);

        var (filas, descartados) = ReglasDimensiones.TransformarClientes(clientes);
        return new DatosDimension<DimCliente>(filas, clientes.Count, descartados);
    }
}
