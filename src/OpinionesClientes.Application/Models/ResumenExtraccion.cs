namespace OpinionesClientes.Application.Models;

/// <summary>Resumen de una ejecución completa (un lote) del proceso de extracción.</summary>
public class ResumenExtraccion
{
    public Guid IdLote { get; init; }
    public DateTime Inicio { get; init; }
    public long DuracionTotalMs { get; set; }
    public List<ResultadoExtraccion> Resultados { get; } = new();

    public int TotalExtraidos => Resultados.Sum(r => r.RegistrosExtraidos);
    public int TotalGuardados => Resultados.Sum(r => r.RegistrosGuardados);
    public int TotalRechazados => Resultados.Sum(r => r.RegistrosRechazados);
    public int FuentesConError => Resultados.Count(r => !r.Exitoso);

    /// <summary>Suma de las duraciones individuales: si es mayor que la duración total, hubo paralelismo.</summary>
    public long SumaDuracionesMs => Resultados.Sum(r => r.DuracionMs);
}
