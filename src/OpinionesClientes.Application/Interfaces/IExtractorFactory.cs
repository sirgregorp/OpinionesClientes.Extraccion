namespace OpinionesClientes.Application.Interfaces;

/// <summary>Crea un extractor por cada fuente habilitada en la configuración.</summary>
public interface IExtractorFactory
{
    IReadOnlyList<IExtractor> CrearExtractores();
}
