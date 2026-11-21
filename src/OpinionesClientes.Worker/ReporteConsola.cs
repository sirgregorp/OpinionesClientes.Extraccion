using OpinionesClientes.Application.Models;

namespace OpinionesClientes.Worker;

/// <summary>Muestra en consola el resumen del lote en forma de tabla.</summary>
public static class ReporteConsola
{
    public static void Mostrar(ResumenExtraccion resumen)
    {
        const string linea = "+---------------------------+-----------+---------------------------+-----------+-----------+-----------+----------+--------+";
        Console.WriteLine();
        Console.WriteLine($"  RESUMEN DE EXTRACCIÓN  |  Lote {resumen.IdLote}  |  {resumen.Inicio:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine(linea);
        Console.WriteLine("| Fuente                    | Tipo      | Tabla staging             | Extraídos | Rechazad. | Guardados | Tiempo   | Estado |");
        Console.WriteLine(linea);

        foreach (var r in resumen.Resultados)
        {
            Console.WriteLine(
                $"| {r.Fuente,-25} | {r.TipoFuente,-9} | {r.TablaStaging,-25} | {r.RegistrosExtraidos,9} | {r.RegistrosRechazados,9} | {r.RegistrosGuardados,9} | {r.DuracionMs,5} ms | {r.Estado,-6} |");
        }

        Console.WriteLine(linea);
        Console.WriteLine(
            $"| {"TOTAL",-25} | {"",-9} | {"",-25} | {resumen.TotalExtraidos,9} | {resumen.TotalRechazados,9} | {resumen.TotalGuardados,9} | {resumen.DuracionTotalMs,5} ms | {(resumen.FuentesConError == 0 ? "OK" : "REVISAR"),-6} |");
        Console.WriteLine(linea);
        Console.WriteLine($"  Paralelismo: suma de tiempos individuales {resumen.SumaDuracionesMs} ms vs. tiempo total real {resumen.DuracionTotalMs} ms");

        foreach (var r in resumen.Resultados.Where(r => !r.Exitoso))
            Console.WriteLine($"  ERROR en {r.Fuente}: {r.MensajeError}");

        Console.WriteLine();
    }
}
