using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Application.Services;
using OpinionesClientes.Data;
using OpinionesClientes.Worker;
using OpinionesClientes.Worker.Logging;

// Punto de entrada del Worker Service (.NET 8).
// Configuración: appsettings.json + appsettings.{Entorno}.json + User Secrets (desarrollo) + variables de entorno.
var builder = Host.CreateApplicationBuilder(args);

// ---- Logging: consola + archivo (ILogger) ----
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss.fff ";
});
builder.Logging.AddProvider(new ArchivoLoggerProvider(Path.Combine(AppContext.BaseDirectory, "Logs")));

// ---- Inyección de dependencias por capas ----
builder.Services.Configure<ExtraccionOptions>(builder.Configuration.GetSection(ExtraccionOptions.Seccion));
builder.Services.AddDataLayer(builder.Configuration);                      // Capa de Datos
builder.Services.AddSingleton<IExtractionService, ExtractionService>();    // Capa de Aplicación
builder.Services.AddHostedService<ExtractionWorker>();                     // Worker

var host = builder.Build();
host.Run();
