using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpinionesClientes.Data.Http;

/// <summary>
/// Reintentos con espera exponencial (0.5 s, 1 s, 2 s...) ante fallos temporales de la API:
/// errores de red, 408, 429 y 5xx. Los errores 4xx (por ejemplo, 401) no se reintentan.
/// </summary>
public class RetryHandler : DelegatingHandler
{
    private readonly SocialApiOptions _opciones;
    private readonly ILogger<RetryHandler> _logger;

    public RetryHandler(IOptions<SocialApiOptions> opciones, ILogger<RetryHandler> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                var respuesta = await base.SendAsync(request, cancellationToken);
                if (!EsTransitorio(respuesta.StatusCode) || intento > _opciones.Reintentos)
                    return respuesta;

                _logger.LogWarning("API respondió {Codigo} en {Url}. Reintento {Intento}/{Max}",
                    (int)respuesta.StatusCode, request.RequestUri, intento, _opciones.Reintentos);
                respuesta.Dispose();
            }
            catch (HttpRequestException ex) when (intento <= _opciones.Reintentos)
            {
                _logger.LogWarning("Error de red con la API ({Mensaje}). Reintento {Intento}/{Max}",
                    ex.Message, intento, _opciones.Reintentos);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, intento - 1)), cancellationToken);
        }
    }

    private static bool EsTransitorio(HttpStatusCode codigo) =>
        codigo is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)codigo >= 500;
}
