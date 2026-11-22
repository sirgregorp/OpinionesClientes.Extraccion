namespace OpinionesClientes.Domain.Entities.Dimensiones;

/// <summary>Fila de dbo.Dim_Tiempo: un día del calendario. La llave es la fecha en formato AAAAMMDD.</summary>
public class DimTiempo
{
    public int TiempoKey { get; set; }
    public DateTime Fecha { get; set; }
    public short Anio { get; set; }
    public byte Trimestre { get; set; }
    public byte Mes { get; set; }
    public string NombreMes { get; set; } = string.Empty;
    public string AnioMes { get; set; } = string.Empty;
    public byte Dia { get; set; }

    /// <summary>1 = lunes ... 7 = domingo.</summary>
    public byte DiaSemana { get; set; }
    public string NombreDia { get; set; } = string.Empty;
    public bool EsFinDeSemana { get; set; }
}
