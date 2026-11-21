namespace OpinionesClientes.Data;

/// <summary>Sección "Staging" de appsettings.json: dónde se guardan los datos extraídos.</summary>
public class StagingOptions
{
    public const string Seccion = "Staging";

    /// <summary>"SqlServer" (tablas stg.* en OpinionesStagingDB) o "Archivo" (archivos JSON temporales).</summary>
    public string Destino { get; set; } = "SqlServer";

    /// <summary>Nombre de la cadena de conexión de la base staging.</summary>
    public string Conexion { get; set; } = "StagingDb";

    /// <summary>Carpeta para los archivos temporales cuando Destino = Archivo.</summary>
    public string CarpetaArchivos { get; set; } = "Staging";

    /// <summary>Vacía la tabla staging antes de cada carga (el historial queda en stg.LogExtraccion).</summary>
    public bool LimpiarAntesDeCargar { get; set; } = true;

    /// <summary>Filas por lote en SqlBulkCopy.</summary>
    public int TamanoLoteBulk { get; set; } = 5000;

    public int TimeoutSegundos { get; set; } = 120;
}
