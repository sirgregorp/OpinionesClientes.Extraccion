namespace OpinionesClientes.Domain.Entities.Dimensiones;

/// <summary>Fila de dbo.Dim_Clasificacion (catálogo fijo: Positiva, Neutra, Negativa).</summary>
public class DimClasificacion
{
    public int ClasificacionKey { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>1 = positiva, 0 = neutra, -1 = negativa. Permite promediar el sentimiento.</summary>
    public short ValorSentimiento { get; set; }
}
