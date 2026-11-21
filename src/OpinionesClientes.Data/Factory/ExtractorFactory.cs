using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Data.Extractors;
using OpinionesClientes.Domain.Entities;
using OpinionesClientes.Domain.Enums;

namespace OpinionesClientes.Data.Factory;

/// <summary>
/// Crea un extractor por cada fuente declarada en appsettings.json (sección Extraccion:Fuentes).
/// ESCALABILIDAD: para agregar una fuente nueva basta con declararla en la configuración;
/// el tipo de fuente (Csv, BaseDatos, Api) decide qué extractor genérico se usa
/// y la entidad decide a qué clase se mapean los registros.
/// </summary>
public class ExtractorFactory : IExtractorFactory
{
    // Entidades del dominio disponibles para mapear, por nombre
    private static readonly Dictionary<string, Type> Entidades =
        typeof(Encuesta).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace == typeof(Encuesta).Namespace)
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

    private readonly IServiceProvider _serviceProvider;
    private readonly ExtraccionOptions _opciones;
    private readonly ILogger<ExtractorFactory> _logger;

    public ExtractorFactory(IServiceProvider serviceProvider, IOptions<ExtraccionOptions> opciones,
        ILogger<ExtractorFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _opciones = opciones.Value;
        _logger = logger;
    }

    public IReadOnlyList<IExtractor> CrearExtractores()
    {
        var extractores = new List<IExtractor>();

        foreach (var fuente in _opciones.Fuentes)
        {
            if (!fuente.Habilitada)
            {
                _logger.LogInformation("Fuente {Fuente} deshabilitada en la configuración; se omite", fuente.Nombre);
                continue;
            }

            if (!Entidades.TryGetValue(fuente.Entidad, out var tipoEntidad))
            {
                _logger.LogError("Fuente {Fuente}: la entidad '{Entidad}' no existe en el dominio; se omite",
                    fuente.Nombre, fuente.Entidad);
                continue;
            }

            if (!Enum.TryParse<TipoFuente>(fuente.Tipo, ignoreCase: true, out var tipo))
            {
                _logger.LogError("Fuente {Fuente}: tipo '{Tipo}' no soportado (use Csv, BaseDatos o Api); se omite",
                    fuente.Nombre, fuente.Tipo);
                continue;
            }

            // Extractor genérico según el tipo de fuente, cerrado con la entidad: CsvExtractor<Encuesta>, etc.
            var definicion = tipo switch
            {
                TipoFuente.Csv => typeof(CsvExtractor<>),
                TipoFuente.BaseDatos => typeof(DatabaseExtractor<>),
                TipoFuente.Api => typeof(ApiExtractor<>),
                _ => throw new NotSupportedException(tipo.ToString())
            };

            var extractor = (IExtractor)ActivatorUtilities.CreateInstance(
                _serviceProvider, definicion.MakeGenericType(tipoEntidad), fuente);

            extractores.Add(extractor);
        }

        return extractores;
    }
}
