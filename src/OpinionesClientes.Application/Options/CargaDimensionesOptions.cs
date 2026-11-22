namespace OpinionesClientes.Application.Options;

/// <summary>Sección "CargaDimensiones" de appsettings.json.</summary>
public class CargaDimensionesOptions
{
    public const string Seccion = "CargaDimensiones";

    /// <summary>Si es false, el Worker solo hace la extracción.</summary>
    public bool Habilitada { get; set; } = true;

    /// <summary>Cadena de conexión de donde se leen los datos (OpinionesStagingDB).</summary>
    public string ConexionStaging { get; set; } = "StagingDb";

    /// <summary>Cadena de conexión del Data Warehouse (OpinionesDW).</summary>
    public string ConexionDataWarehouse { get; set; } = "DataWarehouseDb";

    /// <summary>Cantidad de dimensiones que se cargan a la vez (son tablas independientes entre sí).</summary>
    public int MaxParalelismo { get; set; } = 3;

    public int TimeoutSegundos { get; set; } = 120;
}
