# Sistema de Análisis de Opiniones de Clientes — Proceso ETL

Worker Service en **.NET 8** del proyecto *Sistema de Análisis de Opiniones de Clientes*. Hace dos etapas:

1. **Extracción** (Actividad 1): lee las opiniones de tres tipos de fuente y las deja, sin transformar, en tablas **staging** de SQL Server (o en archivos JSON temporales).
2. **Carga de dimensiones** (Actividad 2): toma los datos de staging, los limpia y carga las dimensiones del Data Warehouse **OpinionesDW** (`Dim_Tiempo`, `Dim_Cliente`, `Dim_Producto`, `Dim_Fuente`, `Dim_Clasificacion`).

**Asignatura:** Electiva 1 (Big Data), ITLA, período 2026-C-3, profesor Francis Ramírez
**Estudiante:** Gregory Mota Alcántara, matrícula 2020-10990

| Fuente | Tipo | Componente | Destino staging |
|---|---|---|---|
| Encuestas internas (`surveys_part1.csv`) | CSV | `CsvExtractor<Encuesta>` (CsvHelper) | `stg.Encuestas` |
| Reseñas del sitio web (`SitioWebDB.dbo.Resenas`) | Base de datos relacional | `DatabaseExtractor<ResenaWeb>` (ADO.NET) | `stg.ResenasWeb` |
| Comentarios de redes sociales | API REST | `ApiExtractor<ComentarioSocial>` (IHttpClientFactory) | `stg.ComentariosSociales` |
| Clientes, productos y registro de fuentes | CSV | `CsvExtractor<Cliente/Producto/FuenteDatos>` | `stg.Clientes`, `stg.Productos`, `stg.FuentesDatos` |

## Arquitectura

```
OpinionesClientes.Extraccion.sln
├── database/
│   ├── 00_CrearSitioWebDB.sql            → BD origen del sitio web (200 reseñas)
│   ├── 01_CrearStagingDB.sql             → OpinionesStagingDB: esquema stg + bitácora
│   ├── 02_ConsultasValidacionStaging.sql → consultas para validar la extracción
│   ├── 03_CrearDataWarehouse.sql         → OpinionesDW (modelo estrella) + bitácora dbo.LogCargaDimensiones
│   └── 04_ConsultasValidacionDW.sql      → consultas para validar las dimensiones cargadas
├── scripts/
│   ├── configurar-secretos.ps1           → guarda la API key en User Secrets
│   └── ejecutar-extraccion.ps1           → inicia la API y ejecuta el Worker
└── src/
    ├── OpinionesClientes.Domain          → Entidades (Encuesta, ResenaWeb, ComentarioSocial...) y Entities/Dimensiones (DimCliente, DimTiempo...)
    ├── OpinionesClientes.Application     → Contratos, orquestadores ExtractionService y DimensionLoadService,
    │                                       Transformaciones/ReglasDimensiones (limpieza y calendario)
    ├── OpinionesClientes.Data            → Implementaciones: lectores, extractores, fábricas, staging
    │   ├── Interfaces/                   → IFileReaderRepository<TClass>, IDatabaseReaderRepository<TClass>, IApiReaderRepository<TClass>, IFileFactory
    │   ├── Repositories/                 → CsvFileReaderRepository, JsonFileReaderRepository, SqlDatabaseReaderRepository,
    │   │                                   ApiReaderRepository, SqlStagingRepository, FileStagingRepository
    │   ├── Factory/                      → FileFactory (crea lectores), ExtractorFactory (crea extractores desde appsettings.json)
    │   ├── Extractors/                   → ExtractorBase, CsvExtractor, DatabaseExtractor, ApiExtractor
    │   ├── Loaders/                      → DimensionLoaderBase y un cargador por dimensión
    │   ├── Repositories/SqlDataWarehouseRepository → SqlBulkCopy a tabla temporal + MERGE en OpinionesDW
    │   └── Http/                         → ApiKeyHandler, RetryHandler
    ├── OpinionesClientes.Worker          → Worker Service: Program.cs (DI), ExtractionWorker, logging a consola y archivo
    └── OpinionesClientes.SocialApi       → API REST que simula la plataforma de redes sociales (requiere X-Api-Key)
```

Las dependencias van hacia adentro (Clean Architecture): `Worker → Data → Application → Domain`. La capa de Aplicación solo conoce interfaces; la capa de Datos las implementa.

## Requisitos

- Visual Studio 2022 (o el SDK de .NET 8 o superior)
- SQL Server Express (`.\SQLEXPRESS`) y SQL Server Management Studio

## Cómo ejecutarlo

1. **Bases de datos.** En SSMS ejecuta, en orden:
   - `database/00_CrearSitioWebDB.sql` (si `SitioWebDB` ya existe de la práctica anterior, puedes omitirlo)
   - `database/01_CrearStagingDB.sql`
   - `database/03_CrearDataWarehouse.sql` (no borra nada: si `OpinionesDW` ya existe solo agrega lo que falta)
2. **Ejecutar.** Abre `OpinionesClientes.Extraccion.sln` en Visual Studio, elige el perfil de inicio **"API redes sociales + Worker de extracción"** en la barra de herramientas (junto al botón de iniciar) y presiona **F5**. Ese perfil está en `OpinionesClientes.Extraccion.slnLaunch` e inicia la API y el Worker a la vez.
   O todo desde PowerShell (crea las bases, compila y ejecuta): `.\scripts\demo-completa.bat`
3. **API key.** En desarrollo, `launchSettings.json` pasa a la API y al Worker la variable de entorno `SocialApi__ApiKey` con una clave local de prueba, válida solo para la API simulada. En un servidor real la clave se define como variable de entorno o con User Secrets (`scripts\configurar-secretos.ps1`); `appsettings.json` nunca la contiene.
4. **Validar.** En SSMS ejecuta `database/02_ConsultasValidacionStaging.sql` (staging) y `database/04_ConsultasValidacionDW.sql` (dimensiones).

Para cargar solo las dimensiones con lo que ya hay en staging, pon `"Habilitada": false` en la sección `Extraccion`.

Si tu instancia de SQL Server no es `.\SQLEXPRESS`, cambia `Server=` en `src/OpinionesClientes.Worker/appsettings.json`.

### Sin SQL Server

Cambia `"Destino": "SqlServer"` por `"Destino": "Archivo"` en la sección `Staging`. Los datos quedan en `bin/Debug/net8.0/Staging/lote_<id>/stg.*.json` (la fuente de base de datos mostrará error si SQL Server no está disponible, sin detener a las demás).

## Agregar una fuente nueva

Solo se agrega un bloque en `appsettings.json`. Ejemplo:

```json
{
  "Nombre": "EncuestasParte2",
  "Tipo": "Csv",
  "Entidad": "Encuesta",
  "Ruta": "Data/surveys_part2.csv",
  "TablaStaging": "stg.Encuestas"
}
```

`ExtractorFactory` crea el extractor adecuado según `Tipo` (`Csv`, `BaseDatos` o `Api`) y `Entidad`. No se modifica el código del orquestador.

## Carga de dimensiones

| Dimensión | Origen en staging | Reglas principales | Llave de negocio |
|---|---|---|---|
| `Dim_Tiempo` | Rango de fechas de encuestas, reseñas y comentarios | Se genera el calendario de los años completos; lunes = 1; nombres en español | `TiempoKey` (AAAAMMDD) |
| `Dim_Cliente` | `stg.Clientes` | Id numérico obligatorio, nombre sin espacios de más, correo en minúsculas (inválido → NULL), duplicados fuera, miembro **Anónimo** | `IdClienteOrigen` |
| `Dim_Producto` | `stg.Productos` | Id numérico obligatorio, categoría vacía → "Sin categoría" | `IdProductoOrigen` |
| `Dim_Fuente` | Fuentes distintas de las tres tablas de opiniones | Tipo y canal según la tabla (Encuestas, Reseñas web, Redes sociales) | `NombreFuente` |
| `Dim_Clasificacion` | `stg.Encuestas` | Catálogo fijo Positiva/Neutra/Negativa con valor 1/0/-1; avisa si llega un valor desconocido | `ClasificacionKey` |

Cada cargador sigue la misma plantilla (`DimensionLoaderBase`): lee staging con `IDatabaseReaderRepository`, aplica las reglas y llama a `SqlDataWarehouseRepository`, que copia las filas a una tabla temporal con `SqlBulkCopy` y ejecuta un `MERGE` (inserta lo nuevo, actualiza solo lo que cambió, SCD tipo 1). Las llaves sustitutas no cambian, así que los hechos ya cargados siguen siendo válidos y el proceso se puede ejecutar varias veces sin duplicar. Las cinco dimensiones se cargan en paralelo y el resultado queda en `dbo.LogCargaDimensiones`.

## Atributos de calidad

| Atributo | Cómo se cumple |
|---|---|
| **Rendimiento** | Todo es asíncrono (`async/await`). Las fuentes se extraen en paralelo con `Task.WhenAll` y un `SemaphoreSlim` (`MaxParalelismo`). CSV leído en streaming con CsvHelper, carga con `SqlBulkCopy`. Cada fuente se mide con `Stopwatch`. |
| **Escalabilidad** | Fuentes declaradas en `appsettings.json`; extractores genéricos (`CsvExtractor<T>`...) creados por `ExtractorFactory`; `FileFactory` admite nuevos formatos (CSV, JSON). |
| **Seguridad** | Autenticación de Windows en las cadenas de conexión (sin contraseñas). API key por variable de entorno / User Secrets, nunca en appsettings.json. Consultas parametrizadas y validación del nombre de tabla staging. Comparación de la API key en tiempo constante. |
| **Mantenibilidad** | Capas separadas (Domain, Application, Data, Worker), interfaces, inyección de dependencias, patrones Factory y Template Method, principios SOLID. |

## Logs

- Consola y archivo `bin/Debug/net8.0/Logs/extraccion_AAAAMMDD.log` (ILogger).
- Tabla `stg.LogExtraccion`: una fila por fuente y ejecución, con registros, duración, estado y error.
- Tabla `OpinionesDW.dbo.LogCargaDimensiones` (vista `dbo.vw_UltimaCargaDimensiones`): una fila por dimensión y ejecución.
