namespace OpinionesClientes.Application.Models;

/// <summary>Resultado de cargar una dimensión: se muestra en consola y se guarda en dbo.LogCargaDimensiones.</summary>
public class ResultadoCargaDimension
{
    public Guid IdLote { get; init; }
    public string Dimension { get; init; } = string.Empty;
    public string TablaDestino { get; init; } = string.Empty;
    public string TablaOrigen { get; init; } = string.Empty;

    /// <summary>Filas leídas de staging (o generadas, en el caso del calendario).</summary>
    public int RegistrosLeidos { get; set; }

    /// <summary>Filas que no pasaron las reglas de limpieza (id inválido, nombre vacío, duplicado...).</summary>
    public int RegistrosDescartados { get; set; }

    public int Insertados { get; set; }
    public int Actualizados { get; set; }
    public int SinCambios { get; set; }

    /// <summary>Total de filas en la dimensión después de la carga.</summary>
    public int TotalEnDimension { get; set; }

    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public long DuracionMs { get; set; }

    public bool Exitoso { get; set; }
    public string? MensajeError { get; set; }

    public string Estado => Exitoso ? "OK" : "ERROR";
}

/// <summary>Conteos que devuelve el MERGE de una dimensión.</summary>
public record ResultadoMerge(int Insertados, int Actualizados, int SinCambios, int TotalEnDimension);

/// <summary>Resumen de una ejecución completa de la carga de dimensiones.</summary>
public class ResumenCargaDimensiones
{
    public Guid IdLote { get; init; }
    public DateTime Inicio { get; init; }
    public long DuracionTotalMs { get; set; }
    public List<ResultadoCargaDimension> Resultados { get; } = new();

    public int TotalLeidos => Resultados.Sum(r => r.RegistrosLeidos);
    public int TotalDescartados => Resultados.Sum(r => r.RegistrosDescartados);
    public int TotalInsertados => Resultados.Sum(r => r.Insertados);
    public int TotalActualizados => Resultados.Sum(r => r.Actualizados);
    public int TotalSinCambios => Resultados.Sum(r => r.SinCambios);
    public int DimensionesConError => Resultados.Count(r => !r.Exitoso);
    public long SumaDuracionesMs => Resultados.Sum(r => r.DuracionMs);
}
