using System.Globalization;
using System.Text;
using OpinionesClientes.Application.Models;
using OpinionesClientes.Domain.Entities;
using OpinionesClientes.Domain.Entities.Dimensiones;

namespace OpinionesClientes.Application.Transformaciones;

/// <summary>
/// Reglas de limpieza y transformación de cada dimensión (la "T" antes de la carga).
/// Son funciones puras: reciben lo que hay en staging y devuelven las filas listas
/// para el Data Warehouse, sin tocar la base de datos.
/// </summary>
public static class ReglasDimensiones
{
    public const string NombreAnonimo = "Anónimo";
    public const string SinCategoria = "Sin categoría";

    private static readonly string[] Meses =
        { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio",
          "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

    private static readonly string[] Dias =
        { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };

    // ------------------------------------------------------------------ Clientes

    /// <summary>
    /// Clientes del CSV + el miembro "Anónimo".
    /// Se descartan filas sin id numérico o sin nombre, y los ids repetidos (se queda el último).
    /// </summary>
    public static (List<DimCliente> Filas, int Descartados) TransformarClientes(IEnumerable<Cliente> origen)
    {
        var porId = new Dictionary<int, DimCliente>();
        var descartados = 0;

        foreach (var c in origen)
        {
            var id = ObtenerId(c.IdCliente);
            var nombre = LimpiarTexto(c.Nombre, 150);
            if (id is null || nombre is null)
            {
                descartados++;
                continue;
            }

            if (porId.ContainsKey(id.Value)) descartados++;
            porId[id.Value] = new DimCliente
            {
                IdClienteOrigen = id,
                Nombre = nombre,
                Email = LimpiarEmail(c.Email)
            };
        }

        var filas = porId.Values.OrderBy(c => c.IdClienteOrigen).ToList();

        // Miembro especial para los comentarios de redes sociales que no tienen cliente
        filas.Insert(0, new DimCliente { IdClienteOrigen = null, Nombre = NombreAnonimo, Email = null });
        return (filas, descartados);
    }

    // ----------------------------------------------------------------- Productos

    public static (List<DimProducto> Filas, int Descartados) TransformarProductos(IEnumerable<Producto> origen)
    {
        var porId = new Dictionary<int, DimProducto>();
        var descartados = 0;

        foreach (var p in origen)
        {
            var id = ObtenerId(p.IdProducto);
            var nombre = LimpiarTexto(p.Nombre, 150);
            if (id is null || nombre is null)
            {
                descartados++;
                continue;
            }

            if (porId.ContainsKey(id.Value)) descartados++;
            porId[id.Value] = new DimProducto
            {
                IdProductoOrigen = id.Value,
                Nombre = nombre,
                Categoria = LimpiarTexto(p.Categoria, 100) ?? SinCategoria
            };
        }

        return (porId.Values.OrderBy(p => p.IdProductoOrigen).ToList(), descartados);
    }

    // ------------------------------------------------------------------- Fuentes

    /// <summary>
    /// Cada fuente distinta que aparece en las opiniones. El tipo y el canal
    /// dependen de la tabla staging donde se encontró.
    /// </summary>
    public static (List<DimFuente> Filas, int Descartados) TransformarFuentes(IEnumerable<OrigenFuente> origen)
    {
        var porNombre = new Dictionary<string, DimFuente>(StringComparer.OrdinalIgnoreCase);
        var descartados = 0;

        foreach (var o in origen)
        {
            var nombre = LimpiarTexto(o.Fuente, 100);
            var tipoYCanal = TipoYCanal(o.TipoOrigen);
            if (nombre is null || tipoYCanal is null || porNombre.ContainsKey(nombre))
            {
                descartados++;
                continue;
            }

            porNombre[nombre] = new DimFuente
            {
                NombreFuente = nombre,
                TipoFuente = tipoYCanal.Value.Tipo,
                Canal = tipoYCanal.Value.Canal
            };
        }

        return (porNombre.Values.OrderBy(f => f.Canal).ThenBy(f => f.NombreFuente).ToList(), descartados);
    }

    /// <summary>Tipo de fuente y canal según la tabla de origen.</summary>
    public static (string Tipo, string Canal)? TipoYCanal(string? tipoOrigen) =>
        tipoOrigen?.Trim().ToLowerInvariant() switch
        {
            "encuesta" => ("CSV", "Encuestas"),
            "resenaweb" => ("Web", "Reseñas web"),
            "redsocial" => ("Red Social", "Redes sociales"),
            _ => null
        };

    // ------------------------------------------------------------- Clasificación

    /// <summary>
    /// Catálogo fijo de clasificaciones. Se cuentan como descartados los valores
    /// de staging que no corresponden a ninguna de las tres.
    /// </summary>
    public static (List<DimClasificacion> Filas, int Descartados) TransformarClasificaciones(IEnumerable<string?> valoresEnStaging)
    {
        var descartados = valoresEnStaging.Count(v => ClasificacionKeyDe(v) is null);

        var filas = new List<DimClasificacion>
        {
            new() { ClasificacionKey = 1, Nombre = "Positiva", ValorSentimiento = 1 },
            new() { ClasificacionKey = 2, Nombre = "Neutra",   ValorSentimiento = 0 },
            new() { ClasificacionKey = 3, Nombre = "Negativa", ValorSentimiento = -1 }
        };
        return (filas, descartados);
    }

    /// <summary>Acepta variaciones como "positivo", "NEGATIVA" o " neutral ".</summary>
    public static int? ClasificacionKeyDe(string? valor)
    {
        var v = QuitarTildes(valor ?? string.Empty).Trim().ToLowerInvariant();
        if (v.StartsWith("positiv")) return 1;
        if (v.StartsWith("neutr")) return 2;
        if (v.StartsWith("negativ")) return 3;
        return null;
    }

    // ------------------------------------------------------------------ Tiempo

    /// <summary>
    /// Genera el calendario completo de los años que cubren las opiniones
    /// (del 1 de enero del primer año al 31 de diciembre del último).
    /// </summary>
    public static List<DimTiempo> GenerarCalendario(DateTime fechaMinima, DateTime fechaMaxima)
    {
        if (fechaMaxima < fechaMinima)
            throw new ArgumentException("La fecha máxima no puede ser menor que la mínima.");

        var desde = new DateTime(fechaMinima.Year, 1, 1);
        var hasta = new DateTime(fechaMaxima.Year, 12, 31);
        var dias = new List<DimTiempo>((hasta - desde).Days + 1);

        for (var f = desde; f <= hasta; f = f.AddDays(1))
        {
            // DayOfWeek empieza en domingo = 0; aquí se usa lunes = 1 ... domingo = 7
            var diaSemana = f.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)f.DayOfWeek;

            dias.Add(new DimTiempo
            {
                TiempoKey = f.Year * 10000 + f.Month * 100 + f.Day,
                Fecha = f,
                Anio = (short)f.Year,
                Trimestre = (byte)((f.Month - 1) / 3 + 1),
                Mes = (byte)f.Month,
                NombreMes = Meses[f.Month - 1],
                AnioMes = f.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Dia = (byte)f.Day,
                DiaSemana = (byte)diaSemana,
                NombreDia = Dias[diaSemana - 1],
                EsFinDeSemana = diaSemana >= 6
            });
        }
        return dias;
    }

    // ---------------------------------------------------------------- Auxiliares

    /// <summary>
    /// Convierte el código de origen en número: "7" → 7, "C007" → 7, "P016" → 16.
    /// Devuelve null si no hay dígitos o el número no es válido.
    /// </summary>
    public static int? ObtenerId(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        var digitos = new string(codigo.Where(char.IsDigit).ToArray());
        return int.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }

    /// <summary>Quita espacios de más; texto vacío → null; recorta al largo de la columna.</summary>
    public static string? LimpiarTexto(string? texto, int largoMaximo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return limpio.Length <= largoMaximo ? limpio : limpio[..largoMaximo];
    }

    /// <summary>Correo en minúsculas; si no tiene un formato mínimo válido se guarda como null.</summary>
    public static string? LimpiarEmail(string? email)
    {
        var e = LimpiarTexto(email, 150)?.ToLowerInvariant();
        if (e is null) return null;
        var arroba = e.IndexOf('@');
        return arroba > 0 && e.IndexOf('.', arroba) > arroba + 1 && !e.Contains(' ') ? e : null;
    }

    private static string QuitarTildes(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }
}
