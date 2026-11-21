using Microsoft.Extensions.Logging;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Data.Interfaces;

namespace OpinionesClientes.Data.Extractors;

/// <summary>Extrae datos de una base de datos relacional (reseñas del sitio web en SitioWebDB).</summary>
public class DatabaseExtractor<TClass> : ExtractorBase<TClass> where TClass : class
{
    private readonly IFileFactory _fileFactory;

    public DatabaseExtractor(FuenteOptions fuente, IFileFactory fileFactory, IStagingRepository staging,
        ILogger<DatabaseExtractor<TClass>> logger) : base(fuente, staging, logger)
    {
        _fileFactory = fileFactory;
    }

    protected override string TipoFuente => "BaseDatos";

    protected override string DescribirOrigen() => $"ConnectionStrings:{Fuente.Conexion}";

    protected override async Task<LecturaFuente<TClass>> LeerAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Fuente.Conexion) || string.IsNullOrWhiteSpace(Fuente.Consulta))
            throw new InvalidOperationException($"La fuente {Fuente.Nombre} requiere 'Conexion' y 'Consulta'.");

        var lector = _fileFactory.CreateDatabaseReader<TClass>();
        var registros = await lector.ReadDataAsync(Fuente.Conexion, Fuente.Consulta, cancellationToken);

        var rechazados = lector is IRegistraRechazos r ? r.RegistrosRechazados : 0;
        return new LecturaFuente<TClass>(registros.ToList(), rechazados);
    }
}
