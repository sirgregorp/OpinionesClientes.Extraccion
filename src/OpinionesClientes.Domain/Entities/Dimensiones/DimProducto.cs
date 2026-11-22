namespace OpinionesClientes.Domain.Entities.Dimensiones;

/// <summary>Fila de dbo.Dim_Producto. ProductoKey es la llave sustituta (IDENTITY).</summary>
public class DimProducto
{
    public int IdProductoOrigen { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
}
