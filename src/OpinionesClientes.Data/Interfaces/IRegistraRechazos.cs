namespace OpinionesClientes.Data.Interfaces;

/// <summary>Lo implementan los lectores que descartan registros inválidos durante la lectura.</summary>
public interface IRegistraRechazos
{
    int RegistrosRechazados { get; }
}
