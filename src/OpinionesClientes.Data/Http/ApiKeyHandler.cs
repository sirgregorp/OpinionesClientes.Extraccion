using Microsoft.Extensions.Options;

namespace OpinionesClientes.Data.Http;

/// <summary>
/// SEGURIDAD: agrega la API key a cada llamada. La clave sale de la configuración
/// protegida (User Secrets / variable de entorno) y nunca se escribe en el log.
/// </summary>
public class ApiKeyHandler : DelegatingHandler
{
    private readonly SocialApiOptions _opciones;

    public ApiKeyHandler(IOptions<SocialApiOptions> opciones)
    {
        _opciones = opciones.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_opciones.ApiKey))
            throw new InvalidOperationException(
                "No se configuró SocialApi:ApiKey. Defínala con 'dotnet user-secrets' o con la variable de entorno SocialApi__ApiKey.");

        request.Headers.Remove(_opciones.NombreCabeceraApiKey);
        request.Headers.Add(_opciones.NombreCabeceraApiKey, _opciones.ApiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
