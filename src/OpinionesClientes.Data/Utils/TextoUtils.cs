using System.Globalization;
using System.Text;

namespace OpinionesClientes.Data.Utils;

public static class TextoUtils
{
    /// <summary>
    /// Normaliza un nombre de columna para compararlo con el nombre de una propiedad:
    /// quita tildes, espacios y guiones bajos, y pasa a minúsculas.
    /// Así "PuntajeSatisfacción" del CSV coincide con la propiedad PuntajeSatisfaccion.
    /// </summary>
    public static string NormalizarNombre(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var descompuesto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c is ' ' or '_' or '-') continue;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
