namespace OpinionesClientes.Domain.Entities;

/// <summary>Reseña publicada en el sitio web (tabla dbo.Resenas de SitioWebDB).</summary>
public class ResenaWeb
{
    public string IdReview { get; set; } = string.Empty;
    public string? IdCliente { get; set; }
    public string? IdProducto { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Comentario { get; set; }
    public int? Rating { get; set; }
}
