namespace OpinionesClientes.Domain.Entities.Dimensiones;

/// <summary>Fila de dbo.Dim_Fuente: de dónde vino la opinión.</summary>
public class DimFuente
{
    /// <summary>Nombre de la fuente tal como llega (EncuestaInterna, SitioWeb, Instagram...).</summary>
    public string NombreFuente { get; set; } = string.Empty;

    /// <summary>Tipo de fuente: CSV, Web o Red Social.</summary>
    public string TipoFuente { get; set; } = string.Empty;

    /// <summary>Canal de análisis: Encuestas, Reseñas web o Redes sociales.</summary>
    public string Canal { get; set; } = string.Empty;
}
