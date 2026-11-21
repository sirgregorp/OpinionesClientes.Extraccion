namespace OpinionesClientes.Application.Models;

/// <summary>Metadatos que acompañan a cada registro guardado en staging (trazabilidad).</summary>
public record ContextoLote(Guid IdLote, DateTime FechaExtraccion, string Fuente, string Origen);
