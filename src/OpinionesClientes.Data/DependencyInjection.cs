using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpinionesClientes.Application.Interfaces;
using OpinionesClientes.Application.Options;
using OpinionesClientes.Data.Factory;
using OpinionesClientes.Data.Http;
using OpinionesClientes.Data.Interfaces;
using OpinionesClientes.Data.Loaders;
using OpinionesClientes.Data.Repositories;

namespace OpinionesClientes.Data;

/// <summary>Registro de los servicios de la capa de Datos en el contenedor de inyección de dependencias.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services, IConfiguration configuracion)
    {
        services.Configure<SocialApiOptions>(configuracion.GetSection(SocialApiOptions.Seccion));
        services.Configure<StagingOptions>(configuracion.GetSection(StagingOptions.Seccion));

        // HttpClient administrado por IHttpClientFactory (reutiliza conexiones, evita agotar sockets)
        services.AddTransient<RetryHandler>();
        services.AddTransient<ApiKeyHandler>();
        services.AddHttpClient(SocialApiOptions.NombreCliente, (sp, cliente) =>
            {
                var api = sp.GetRequiredService<IOptions<SocialApiOptions>>().Value;
                cliente.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + "/");
                cliente.Timeout = TimeSpan.FromSeconds(api.TimeoutSegundos);
                cliente.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            .AddHttpMessageHandler<RetryHandler>()
            .AddHttpMessageHandler<ApiKeyHandler>();

        // Fábricas
        services.AddSingleton<IFileFactory, FileFactory>();
        services.AddSingleton<IExtractorFactory, ExtractorFactory>();

        // Destino staging según la configuración: tablas SQL o archivos temporales
        var destino = configuracion.GetSection(StagingOptions.Seccion)["Destino"] ?? "SqlServer";
        if (destino.Equals("Archivo", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IStagingRepository, FileStagingRepository>();
        else
            services.AddSingleton<IStagingRepository, SqlStagingRepository>();

        // Carga de dimensiones del Data Warehouse: un cargador por dimensión
        services.Configure<CargaDimensionesOptions>(configuracion.GetSection(CargaDimensionesOptions.Seccion));
        services.AddSingleton<IDataWarehouseRepository, SqlDataWarehouseRepository>();
        services.AddSingleton<IDimensionLoader, TiempoDimensionLoader>();
        services.AddSingleton<IDimensionLoader, ClienteDimensionLoader>();
        services.AddSingleton<IDimensionLoader, ProductoDimensionLoader>();
        services.AddSingleton<IDimensionLoader, FuenteDimensionLoader>();
        services.AddSingleton<IDimensionLoader, ClasificacionDimensionLoader>();

        return services;
    }
}
