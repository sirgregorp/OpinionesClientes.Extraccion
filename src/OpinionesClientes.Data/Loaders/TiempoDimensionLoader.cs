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
/// Dim_Tiempo: no se copia de ninguna tabla, se genera. Se toma la fecha mínima y máxima
/// de las opiniones en staging y se crea el calendario de esos años completos.
/// </summary>
public class TiempoDimensionLoader : DimensionLoaderBase<DimTiempo>
{
    private const string Consulta = """
        SELECT MIN(Fecha) AS FechaMinima, MAX(Fecha) AS FechaMaxima
        FROM (
            SELECT Fecha FROM stg.Encuestas
            UNION ALL SELECT Fecha FROM stg.ResenasWeb
            UNION ALL SELECT Fecha FROM stg.ComentariosSociales
        ) AS f
        """;

    public TiempoDimensionLoader(IFileFactory fileFactory, IDataWarehouseRepository dataWarehouse,
        IOptions<CargaDimensionesOptions> opciones, ILogger<TiempoDimensionLoader> logger)
        : base(fileFactory, dataWarehouse, opciones, logger) { }

    public override string Dimension => "Tiempo";
    protected override string TablaDestino => "dbo.Dim_Tiempo";
    protected override string TablaOrigen => "Calendario (rango de fechas de staging)";
    protected override IReadOnlyCollection<string> ColumnasLlave { get; } = new[] { nameof(DimTiempo.TiempoKey) };

    protected override async Task<DatosDimension<DimTiempo>> PrepararAsync(CancellationToken cancellationToken)
    {
        var rango = (await LeerStagingAsync<RangoFechas>(Consulta, cancellationToken)).FirstOrDefault();
        if (rango?.FechaMinima is null || rango.FechaMaxima is null)
            throw new InvalidOperationException("Staging no tiene opiniones con fecha: no se puede generar el calendario.");

        Logger.LogInformation("[Tiempo] Opiniones entre {Desde:yyyy-MM-dd} y {Hasta:yyyy-MM-dd}",
            rango.FechaMinima, rango.FechaMaxima);

        var calendario = ReglasDimensiones.GenerarCalendario(rango.FechaMinima.Value, rango.FechaMaxima.Value);
        return new DatosDimension<DimTiempo>(calendario, calendario.Count, 0);
    }
}
