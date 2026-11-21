namespace OpinionesClientes.Application.Models;

/// <summary>Resultado de extraer una fuente: se registra en el log y en stg.LogExtraccion.</summary>
public class ResultadoExtraccion
{
    public Guid IdLote { get; init; }
    public string Fuente { get; init; } = string.Empty;
    public string TipoFuente { get; init; } = string.Empty;
    public string Entidad { get; init; } = string.Empty;
    public string TablaStaging { get; init; } = string.Empty;
    public string Origen { get; init; } = string.Empty;

    public int RegistrosExtraidos { get; set; }
    public int RegistrosRechazados { get; set; }
    public int RegistrosGuardados { get; set; }

    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public long DuracionMs { get; set; }

    public bool Exitoso { get; set; }
    public string? MensajeError { get; set; }

    public string Estado => Exitoso ? "OK" : "ERROR";
}
