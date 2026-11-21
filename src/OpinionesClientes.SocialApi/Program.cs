using System.Security.Cryptography;
using System.Text;
using OpinionesClientes.SocialApi;

// API REST simulada de redes sociales (Instagram, Facebook, Twitter).
// Es la fuente "API REST" del proceso de extracción.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ComentariosRepository>();

var app = builder.Build();
var apiKey = app.Configuration["SocialApi:ApiKey"];

if (string.IsNullOrWhiteSpace(apiKey))
    app.Logger.LogWarning("SocialApi:ApiKey no está configurada: todas las llamadas a /api serán rechazadas.");

app.MapGet("/health", () => Results.Ok(new { estado = "OK", servicio = "SocialApi" }));

// SEGURIDAD: todo /api exige la cabecera X-Api-Key
var api = app.MapGroup("/api/v1").AddEndpointFilter(async (contexto, siguiente) =>
{
    var recibida = contexto.HttpContext.Request.Headers["X-Api-Key"].ToString();
    if (string.IsNullOrWhiteSpace(apiKey) || !ClavesIguales(recibida, apiKey))
        return Results.Problem("API key ausente o inválida.", statusCode: StatusCodes.Status401Unauthorized);

    return await siguiente(contexto);
});

// GET /api/v1/comentarios?pagina=1&tamanoPagina=50
api.MapGet("/comentarios", (ComentariosRepository repositorio, int pagina = 1, int tamanoPagina = 50) =>
{
    pagina = Math.Max(1, pagina);
    tamanoPagina = Math.Clamp(tamanoPagina, 1, 500);

    var total = repositorio.Comentarios.Count;
    var totalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina);
    var datos = repositorio.Comentarios.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList();

    return Results.Ok(new PaginaDto<ComentarioDto>(pagina, tamanoPagina, total, totalPaginas, datos));
});

app.Run();

// Comparación en tiempo constante para no filtrar información por tiempos de respuesta
static bool ClavesIguales(string a, string b) =>
    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
