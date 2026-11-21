using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.Extensions.Logging;
using OpinionesClientes.Data.Interfaces;
using OpinionesClientes.Data.Utils;

namespace OpinionesClientes.Data.Repositories;

/// <summary>
/// Lee archivos CSV con CsvHelper y los convierte en entidades del dominio.
/// Valida la estructura (encabezados obligatorios) y descarta, sin detener la lectura,
/// las filas con datos que no se pueden convertir.
/// </summary>
public class CsvFileReaderRepository<TClass> : IFileReaderRepository<TClass>, IRegistraRechazos
    where TClass : class
{
    private readonly ILogger<CsvFileReaderRepository<TClass>> _logger;

    public int RegistrosRechazados { get; private set; }

    public CsvFileReaderRepository(ILogger<CsvFileReaderRepository<TClass>> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<TClass>> ReadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"No se encontró el archivo CSV '{filePath}'.", filePath);

        RegistrosRechazados = 0;
        var nombreArchivo = Path.GetFileName(filePath);

        var configuracion = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            // "Categoría" y "Categoria" se consideran el mismo encabezado
            PrepareHeaderForMatch = args => TextoUtils.NormalizarNombre(args.Header),
            // Campo con comillas mal cerradas: se registra en el log
            BadDataFound = args => _logger.LogWarning(
                "{Archivo}: dato mal formado en la fila {Fila}: {Registro}",
                nombreArchivo, args.Context.Parser?.RawRow, args.RawRecord.Trim()),
            // Fila que no se puede convertir (fecha o número inválido, columnas faltantes):
            // se descarta y la lectura continúa
            ReadingExceptionOccurred = args =>
            {
                RegistrosRechazados++;
                _logger.LogWarning("{Archivo}: fila {Fila} rechazada. {Motivo}",
                    nombreArchivo, args.Exception.Context?.Parser?.RawRow, DescribirError(args.Exception));
                return false;
            }
        };

        var registros = new List<TClass>();

        // Lectura en streaming y asíncrona: el archivo no se carga completo en memoria
        await using var flujo = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);
        using var lector = new StreamReader(flujo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(lector, configuracion);

        // Si falta una columna obligatoria, CsvHelper lanza HeaderValidationException
        // y la fuente se marca con error (validación de estructura).
        try
        {
            await foreach (var registro in csv.GetRecordsAsync<TClass>(cancellationToken))
                registros.Add(registro);
        }
        catch (HeaderValidationException ex)
        {
            var faltantes = string.Join(", ", ex.InvalidHeaders.SelectMany(h => h.Names));
            throw new InvalidDataException(
                $"El archivo {nombreArchivo} no tiene las columnas obligatorias: {faltantes}.", ex);
        }

        return registros;
    }

    /// <summary>Traduce los errores de CsvHelper a un mensaje claro para el log.</summary>
    private static string DescribirError(CsvHelperException ex) => ex switch
    {
        TypeConverterException tc =>
            $"Valor '{tc.Text}' inválido para la columna {tc.MemberMapData?.Member?.Name}.",
        CsvHelper.MissingFieldException =>
            "La fila tiene menos columnas que el encabezado.",
        _ => ex.Message.Split('\n', 2)[0].Trim()
    };
}
