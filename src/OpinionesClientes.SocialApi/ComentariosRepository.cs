using System.Text;

namespace OpinionesClientes.SocialApi;

/// <summary>Comentario tal como lo expone la API.</summary>
public record ComentarioDto(string IdComment, string? IdCliente, string? IdProducto, string? Fuente, string? Fecha, string? Comentario);

/// <summary>Respuesta paginada de la API.</summary>
public record PaginaDto<T>(int Pagina, int TamanoPagina, int TotalRegistros, int TotalPaginas, IReadOnlyList<T> Datos);

/// <summary>Carga en memoria los comentarios de Data/social_comments.csv al iniciar la API.</summary>
public class ComentariosRepository
{
    public IReadOnlyList<ComentarioDto> Comentarios { get; }

    public ComentariosRepository(IWebHostEnvironment entorno)
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Data", "social_comments.csv");
        if (!File.Exists(ruta))
            ruta = Path.Combine(entorno.ContentRootPath, "Data", "social_comments.csv");

        Comentarios = File.ReadAllLines(ruta, Encoding.UTF8)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(SepararCampos)
            .Select(c => new ComentarioDto(c[0], Vacio(c[1]), Vacio(c[2]), Vacio(c[3]), Vacio(c[4]), Vacio(c[5])))
            .ToList();
    }

    private static string? Vacio(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Separa una línea CSV respetando comas dentro de comillas.</summary>
    private static string[] SepararCampos(string linea)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;

        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (c == '"')
            {
                if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                else entreComillas = !entreComillas;
            }
            else if (c == ',' && !entreComillas) { campos.Add(actual.ToString()); actual.Clear(); }
            else actual.Append(c);
        }
        campos.Add(actual.ToString());

        while (campos.Count < 6) campos.Add(string.Empty);
        return campos.ToArray();
    }
}
