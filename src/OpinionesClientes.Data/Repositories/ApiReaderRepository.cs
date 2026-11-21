using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Data.Interfaces;

namespace OpinionesClientes.Data.Repositories;

/// <summary>Formato de respuesta paginada que devuelve la API de redes sociales.</summary>
public class PaginaApi<TClass>
{
    public int Pagina { get; set; }
    public int TamanoPagina { get; set; }
    public int TotalRegistros { get; set; }
    public int TotalPaginas { get; set; }
    public List<TClass> Datos { get; set; } = new();
}

/// <summary>
/// Consume la API REST de comentarios con IHttpClientFactory.
/// Recorre todas las páginas; la API key y los reintentos los agregan los
/// DelegatingHandler registrados en el HttpClient (ver DependencyInjection).
/// </summary>
public class ApiReaderRepository<TClass> : IApiReaderRepository<TClass> where TClass : class
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SocialApiOptions _opciones;
    private readonly ILogger<ApiReaderRepository<TClass>> _logger;

    public ApiReaderRepository(IHttpClientFactory httpClientFactory, IOptions<SocialApiOptions> opciones,
        ILogger<ApiReaderRepository<TClass>> logger)
    {
        _httpClientFactory = httpClientFactory;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<TClass>> ReadApiAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var cliente = _httpClientFactory.CreateClient(SocialApiOptions.NombreCliente);
        var registros = new List<TClass>();
        var pagina = 1;
        int totalPaginas;

        do
        {
            var url = $"{endpoint}?pagina={pagina}&tamanoPagina={_opciones.TamanoPagina}";
            using var respuesta = await cliente.GetAsync(url, cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"La API respondió {(int)respuesta.StatusCode} {respuesta.ReasonPhrase} en {url}", null, respuesta.StatusCode);

            var contenido = await respuesta.Content.ReadFromJsonAsync<PaginaApi<TClass>>(cancellationToken: cancellationToken)
                            ?? throw new InvalidOperationException($"La API devolvió una respuesta vacía en {url}");

            registros.AddRange(contenido.Datos);
            totalPaginas = contenido.TotalPaginas;

            _logger.LogDebug("API {Endpoint}: página {Pagina}/{Total}, {Cantidad} registros",
                endpoint, pagina, totalPaginas, contenido.Datos.Count);
            pagina++;
        }
        while (pagina <= totalPaginas);

        return registros;
    }
}
