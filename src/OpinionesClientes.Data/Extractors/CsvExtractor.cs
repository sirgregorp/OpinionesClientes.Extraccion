using Microsoft.Extensions.Logging;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Data.Interfaces;

namespace OpinionesClientes.Data.Extractors;

/// <summary>Extrae datos de archivos CSV (encuestas internas, clientes, productos...).</summary>
public class CsvExtractor<TClass> : ExtractorBase<TClass> where TClass : class
{
    private readonly IFileFactory _fileFactory;

    public CsvExtractor(FuenteOptions fuente, IFileFactory fileFactory, IStagingRepository staging,
        ILogger<CsvExtractor<TClass>> logger) : base(fuente, staging, logger)
    {
        _fileFactory = fileFactory;
    }

    protected override string TipoFuente => "CSV";

    private string RutaCompleta =>
        string.IsNullOrWhiteSpace(Fuente.Ruta)
            ? throw new InvalidOperationException($"La fuente {Fuente.Nombre} no tiene 'Ruta' configurada.")
            : Path.IsPathRooted(Fuente.Ruta) ? Fuente.Ruta : Path.Combine(AppContext.BaseDirectory, Fuente.Ruta);

    protected override string DescribirOrigen() => Fuente.Ruta ?? "(sin ruta)";

    protected override async Task<LecturaFuente<TClass>> LeerAsync(CancellationToken cancellationToken)
    {
        var ruta = RutaCompleta;

        // La fábrica entrega el lector adecuado según la extensión del archivo
        var lector = _fileFactory.CreateFileReader<TClass>(ruta);
        var registros = await lector.ReadFileAsync(ruta, cancellationToken);

        var rechazados = lector is IRegistraRechazos r ? r.RegistrosRechazados : 0;
        return new LecturaFuente<TClass>(registros.ToList(), rechazados);
    }
}
