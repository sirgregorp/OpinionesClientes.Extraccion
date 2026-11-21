using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Reflection;
using OpinionesClientes.Application.Models;

namespace OpinionesClientes.Data.Utils;

/// <summary>
/// Mapeo por reflexión entre filas (IDataRecord / DataTable) y entidades.
/// Las propiedades de cada tipo se guardan en caché para no repetir la reflexión (rendimiento).
/// </summary>
public static class ObjectMapper
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> CacheTipos = new();

    public static PropertyInfo[] Propiedades(Type tipo) =>
        CacheTipos.GetOrAdd(tipo, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite).ToArray());

    /// <summary>Relaciona cada columna del lector con la propiedad del mismo nombre (normalizado).</summary>
    public static PropertyInfo?[] MapaColumnas<TClass>(IDataRecord registro)
    {
        var porNombre = Propiedades(typeof(TClass))
            .ToDictionary(p => TextoUtils.NormalizarNombre(p.Name));

        var mapa = new PropertyInfo?[registro.FieldCount];
        for (var i = 0; i < registro.FieldCount; i++)
            mapa[i] = porNombre.GetValueOrDefault(TextoUtils.NormalizarNombre(registro.GetName(i)));
        return mapa;
    }

    /// <summary>Crea una entidad a partir de la fila actual del lector.</summary>
    public static TClass CrearDesde<TClass>(IDataRecord registro, PropertyInfo?[] mapa) where TClass : class
    {
        var entidad = Activator.CreateInstance<TClass>();
        for (var i = 0; i < mapa.Length; i++)
        {
            var propiedad = mapa[i];
            if (propiedad is null || registro.IsDBNull(i)) continue;
            propiedad.SetValue(entidad, Convertir(registro.GetValue(i), propiedad.PropertyType));
        }
        return entidad;
    }

    /// <summary>
    /// Convierte una colección de entidades en un DataTable para SqlBulkCopy,
    /// agregando las columnas de trazabilidad del lote (IdLote, FechaExtraccion, Fuente, Origen).
    /// </summary>
    public static DataTable ADataTable<TClass>(IEnumerable<TClass> registros, ContextoLote contexto)
    {
        var propiedades = Propiedades(typeof(TClass));
        var tabla = new DataTable(typeof(TClass).Name);

        foreach (var p in propiedades)
            tabla.Columns.Add(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType);

        tabla.Columns.Add("IdLote", typeof(Guid));
        tabla.Columns.Add("FechaExtraccion", typeof(DateTime));
        tabla.Columns.Add("FuenteExtraccion", typeof(string));
        tabla.Columns.Add("Origen", typeof(string));

        foreach (var registro in registros)
        {
            var fila = tabla.NewRow();
            foreach (var p in propiedades)
                fila[p.Name] = p.GetValue(registro) ?? DBNull.Value;

            fila["IdLote"] = contexto.IdLote;
            fila["FechaExtraccion"] = contexto.FechaExtraccion;
            fila["FuenteExtraccion"] = contexto.Fuente;
            fila["Origen"] = contexto.Origen;
            tabla.Rows.Add(fila);
        }
        return tabla;
    }

    private static object? Convertir(object valor, Type destino)
    {
        var tipo = Nullable.GetUnderlyingType(destino) ?? destino;
        if (tipo.IsInstanceOfType(valor)) return valor;

        if (tipo == typeof(string))
            return Convert.ToString(valor, CultureInfo.InvariantCulture);

        if (tipo == typeof(DateTime) && valor is string texto)
            return DateTime.Parse(texto, CultureInfo.InvariantCulture);

        return Convert.ChangeType(valor, tipo, CultureInfo.InvariantCulture);
    }
}
