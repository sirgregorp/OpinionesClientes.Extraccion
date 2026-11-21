namespace OpinionesClientes.Domain.Entities;

/// <summary>Producto tal como viene en products.csv.</summary>
public class Producto
{
    public string IdProducto { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? Categoria { get; set; }
}
