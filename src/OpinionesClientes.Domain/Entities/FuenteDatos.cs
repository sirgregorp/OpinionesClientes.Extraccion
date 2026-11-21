namespace OpinionesClientes.Domain.Entities;

/// <summary>Registro de una carga de datos (fuente_datos.csv).</summary>
public class FuenteDatos
{
    public string IdFuente { get; set; } = string.Empty;
    public string? TipoFuente { get; set; }
    public DateTime? FechaCarga { get; set; }
}
