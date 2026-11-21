namespace OpinionesClientes.Domain.Entities;

/// <summary>Encuesta interna de satisfacción (surveys_part1.csv).</summary>
public class Encuesta
{
    public string IdOpinion { get; set; } = string.Empty;
    public string? IdCliente { get; set; }
    public string? IdProducto { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Comentario { get; set; }
    public string? Clasificacion { get; set; }
    public int? PuntajeSatisfaccion { get; set; }
    public string? Fuente { get; set; }
}
