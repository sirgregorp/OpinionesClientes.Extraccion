using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Models;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Application.Transformaciones;
using OpinionesClientes.Data.Interfaces;
using OpinionesClientes.Domain.Entities.Dimensiones;

namespace OpinionesClientes.Data.Loaders;

/// <summary>
/// Dim_Fuente: las fuentes que aparecen en las tres tablas de opiniones.
/// Las reseñas web no traen columna de fuente: todas vienen del sitio web.
/// </summary>
public class FuenteDimensionLoader : DimensionLoaderBase<DimFuente>
{
    private const string Consulta = """
        SELECT DISTINCT Fuente, 'Encuesta' AS TipoOrigen FROM stg.Encuestas
        UNION
        SELECT DISTINCT Fuente, 'RedSocial' FROM stg.ComentariosSociales
        UNION
        SELECT 'SitioWeb', 'ResenaWeb' WHERE EXISTS (SELECT 1 FROM stg.ResenasWeb)
        """;

    public FuenteDimensionLoader(IFileFactory fileFactory, IDataWarehouseRepository dataWarehouse,
        IOptions<CargaDimensionesOptions> opciones, ILogger<FuenteDimensionLoader> logger)
        : base(fileFactory, dataWarehouse, opciones, logger) { }

    public override string Dimension => "Fuente";
    protected override string TablaDestino => "dbo.Dim_Fuente";
    protected override string TablaOrigen => "stg.Encuestas, stg.ResenasWeb, stg.ComentariosSociales";
    protected override IReadOnlyCollection<string> ColumnasLlave { get; } = new[] { nameof(DimFuente.NombreFuente) };

    protected override async Task<DatosDimension<DimFuente>> PrepararAsync(CancellationToken cancellationToken)
    {
        var fuentes = await LeerStagingAsync<OrigenFuente>(Consulta, cancellationToken);
        var (filas, descartados) = ReglasDimensiones.TransformarFuentes(fuentes);
        return new DatosDimension<DimFuente>(filas, fuentes.Count, descartados);
    }
}
