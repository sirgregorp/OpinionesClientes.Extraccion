using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Data.Interfaces;

namespace OpinionesClientes.Data.Extractors;

/// <summary>Extrae datos de una API REST (comentarios de redes sociales).</summary>
public class ApiExtractor<TClass> : ExtractorBase<TClass> where TClass : class
{
    private readonly IFileFactory _fileFactory;
    private readonly SocialApiOptions _api;

    public ApiExtractor(FuenteOptions fuente, IFileFactory fileFactory, IStagingRepository staging,
        IOptions<SocialApiOptions> api, ILogger<ApiExtractor<TClass>> logger) : base(fuente, staging, logger)
    {
        _fileFactory = fileFactory;
        _api = api.Value;
    }

    protected override string TipoFuente => "API";

    protected override string DescribirOrigen() => $"{_api.BaseUrl.TrimEnd('/')}/{Fuente.Endpoint?.TrimStart('/')}";

    protected override async Task<LecturaFuente<TClass>> LeerAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Fuente.Endpoint))
            throw new InvalidOperationException($"La fuente {Fuente.Nombre} no tiene 'Endpoint' configurado.");

        var lector = _fileFactory.CreateApiReader<TClass>();
        var registros = await lector.ReadApiAsync(Fuente.Endpoint, cancellationToken);
        return new LecturaFuente<TClass>(registros.ToList(), 0);
    }
}
