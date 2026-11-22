namespace OpinionesClientes.Domain.Entities.Dimensiones;

/// <summary>
/// Fila de dbo.Dim_Cliente. La llave sustituta (ClienteKey) la genera SQL Server.
/// IdClienteOrigen = null representa al miembro "Anónimo" (comentarios sociales sin cliente).
/// Pais, TipoCliente y RangoEdad no vienen en las fuentes: quedan con su valor por defecto.
/// </summary>
public class DimCliente
{
    public int? IdClienteOrigen { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Email { get; set; }
}
