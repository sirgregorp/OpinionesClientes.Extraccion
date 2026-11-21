namespace OpinionesClientes.Application.Options;

/// <summary>
/// Sección "Extraccion" de appsettings.json.
/// Las fuentes se declaran en configuración: agregar una fuente nueva
/// no requiere modificar el orquestador (principio Abierto/Cerrado).
/// </summary>
public class ExtraccionOptions
{
    public const string Seccion = "Extraccion";

    /// <summary>Cantidad máxima de fuentes que se extraen en paralelo.</summary>
    public int MaxParalelismo { get; set; } = 4;

    /// <summary>Cada cuántos minutos se repite la extracción. 0 = se ejecuta una vez y el servicio termina.</summary>
    public int IntervaloMinutos { get; set; } = 0;

    public List<FuenteOptions> Fuentes { get; set; } = new();
}

/// <summary>Configuración de una fuente de datos.</summary>
public class FuenteOptions
{
    /// <summary>Nombre descriptivo de la fuente (aparece en los logs).</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Csv, BaseDatos o Api.</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Entidad del dominio a la que se mapean los registros (Encuesta, ResenaWeb, ComentarioSocial...).</summary>
    public string Entidad { get; set; } = string.Empty;

    /// <summary>Tabla staging de destino (por ejemplo, stg.Encuestas).</summary>
    public string TablaStaging { get; set; } = string.Empty;

    public bool Habilitada { get; set; } = true;

    // ---- Según el tipo de fuente se usa una de estas propiedades ----

    /// <summary>Csv: ruta del archivo (relativa a la carpeta del ejecutable o absoluta).</summary>
    public string? Ruta { get; set; }

    /// <summary>BaseDatos: nombre de la cadena de conexión en ConnectionStrings.</summary>
    public string? Conexion { get; set; }

    /// <summary>BaseDatos: consulta SELECT que se ejecuta en el origen.</summary>
    public string? Consulta { get; set; }

    /// <summary>Api: ruta relativa del endpoint (la URL base está en la sección SocialApi).</summary>
    public string? Endpoint { get; set; }
}
