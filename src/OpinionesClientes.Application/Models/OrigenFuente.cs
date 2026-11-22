namespace OpinionesClientes.Application.Models;

/// <summary>
/// Una fuente encontrada en staging y la tabla donde apareció
/// (Encuesta, ResenaWeb o RedSocial). Se usa para construir Dim_Fuente.
/// </summary>
public class OrigenFuente
{
    public string? Fuente { get; set; }
    public string? TipoOrigen { get; set; }
}

/// <summary>Fecha mínima y máxima de las opiniones en staging (para generar Dim_Tiempo).</summary>
public class RangoFechas
{
    public DateTime? FechaMinima { get; set; }
    public DateTime? FechaMaxima { get; set; }
}
