using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Application.Transformaciones;
using OpinionesClientes.Data.Interfaces;
using OpinionesClientes.Domain.Entities;
using OpinionesClientes.Domain.Entities.Dimensiones;

namespace OpinionesClientes.Data.Loaders;

/// <summary>
/// Dim_Clasificacion: catálogo fijo (Positiva, Neutra, Negativa).
/// Se revisan los valores que llegan en las encuestas para detectar clasificaciones desconocidas.
/// </summary>
public class ClasificacionDimensionLoader : DimensionLoaderBase<DimClasificacion>
{
    public ClasificacionDimensionLoader(IFileFactory fileFactory, IDataWarehouseRepository dataWarehouse,
        IOptions<CargaDimensionesOptions> opciones, ILogger<ClasificacionDimensionLoader> logger)
        : base(fileFactory, dataWarehouse, opciones, logger) { }

    public override string Dimension => "Clasificacion";
    protected override string TablaDestino => "dbo.Dim_Clasificacion";
    protected override string TablaOrigen => "stg.Encuestas";
    protected override IReadOnlyCollection<string> ColumnasLlave { get; } = new[] { nameof(DimClasificacion.ClasificacionKey) };

    protected override async Task<DatosDimension<DimClasificacion>> PrepararAsync(CancellationToken cancellationToken)
    {
        var encuestas = await LeerStagingAsync<Encuesta>(
            "SELECT DISTINCT Clasificacion FROM stg.Encuestas", cancellationToken);

        var valores = encuestas.Select(e => e.Clasificacion).ToList();
        foreach (var desconocido in valores.Where(v => ReglasDimensiones.ClasificacionKeyDe(v) is null))
            Logger.LogWarning("[Clasificacion] Valor no reconocido en staging: '{Valor}'", desconocido);

        var (filas, descartados) = ReglasDimensiones.TransformarClasificaciones(valores);
        return new DatosDimension<DimClasificacion>(filas, valores.Count, descartados);
    }
}
