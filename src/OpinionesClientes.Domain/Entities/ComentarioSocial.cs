namespace OpinionesClientes.Domain.Entities;

/// <summary>Comentario de redes sociales obtenido desde la API REST.</summary>
public class ComentarioSocial
{
    public string IdComment { get; set; } = string.Empty;
    public string? IdCliente { get; set; }
    public string? IdProducto { get; set; }
    public string? Fuente { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Comentario { get; set; }
}
