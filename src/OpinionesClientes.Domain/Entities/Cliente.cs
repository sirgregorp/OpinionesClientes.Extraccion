namespace OpinionesClientes.Domain.Entities;

/// <summary>Cliente tal como viene en clients.csv.</summary>
public class Cliente
{
    public string IdCliente { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Email { get; set; }
}
