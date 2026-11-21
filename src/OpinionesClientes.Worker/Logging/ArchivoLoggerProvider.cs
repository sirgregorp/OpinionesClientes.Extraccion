using Microsoft.Extensions.Logging;

namespace OpinionesClientes.Worker.Logging;

/// <summary>
/// Proveedor de ILogger que escribe en Logs/extraccion_AAAAMMDD.log.
/// Complementa la consola: deja evidencia permanente de cada ejecución (trazabilidad).
/// </summary>
public sealed class ArchivoLoggerProvider : ILoggerProvider
{
    private readonly string _carpeta;
    private readonly object _bloqueo = new();

    public ArchivoLoggerProvider(string carpeta)
    {
        _carpeta = carpeta;
        Directory.CreateDirectory(_carpeta);
    }

    public ILogger CreateLogger(string categoria) => new ArchivoLogger(this, categoria);

    internal void Escribir(string linea)
    {
        var archivo = Path.Combine(_carpeta, $"extraccion_{DateTime.Now:yyyyMMdd}.log");
        lock (_bloqueo) // varios extractores escriben a la vez
        {
            File.AppendAllText(archivo, linea + Environment.NewLine);
        }
    }

    public void Dispose() { }

    private sealed class ArchivoLogger(ArchivoLoggerProvider proveedor, string categoria) : ILogger
    {
        private readonly string _categoria = categoria.Split('.').Last().Split('`')[0];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel nivel) => nivel >= LogLevel.Information;

        public void Log<TState>(LogLevel nivel, EventId eventId, TState state, Exception? excepcion,
            Func<TState, Exception?, string> formateador)
        {
            if (!IsEnabled(nivel)) return;

            var linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{Abreviar(nivel)}] {_categoria}: {formateador(state, excepcion)}";
            if (excepcion is not null)
                linea += Environment.NewLine + "    " + excepcion.GetType().Name + ": " + excepcion.Message;

            proveedor.Escribir(linea);
        }

        private static string Abreviar(LogLevel nivel) => nivel switch
        {
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => nivel.ToString().ToUpperInvariant()[..3]
        };
    }
}
