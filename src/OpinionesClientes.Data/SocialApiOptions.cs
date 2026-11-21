namespace OpinionesClientes.Data;

/// <summary>Sección "SocialApi" de appsettings.json.</summary>
public class SocialApiOptions
{
    public const string Seccion = "SocialApi";
    public const string NombreCliente = "SocialApi";

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// SEGURIDAD: la API key NO se escribe en appsettings.json. Se toma de User Secrets
    /// (desarrollo) o de la variable de entorno SocialApi__ApiKey (producción).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string NombreCabeceraApiKey { get; set; } = "X-Api-Key";
    public int TamanoPagina { get; set; } = 50;
    public int TimeoutSegundos { get; set; } = 30;
    public int Reintentos { get; set; } = 3;
}
