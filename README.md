# Sistema de Análisis de Opiniones de Clientes — Proceso de Extracción (ETL)

Worker Service en **.NET 8** que implementa la **Extracción** (la "E" del ETL) del proyecto *Sistema de Análisis de Opiniones de Clientes*. Lee las opiniones de tres tipos de fuente y las deja, sin transformar, en tablas **staging** de SQL Server (o en archivos JSON temporales).

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
│   └── 02_ConsultasValidacionStaging.sql → consultas para validar la extracción
├── scripts/
│   ├── configurar-secretos.ps1           → guarda la API key en User Secrets
│   └── ejecutar-extraccion.ps1           → inicia la API y ejecuta el Worker
└── src/
    ├── OpinionesClientes.Domain          → Entidades (Encuesta, ResenaWeb, ComentarioSocial, Cliente, Producto, FuenteDatos)
    ├── OpinionesClientes.Application     → Contratos (IExtractor, IStagingRepository...) y orquestador ExtractionService
    ├── OpinionesClientes.Data            → Implementaciones: lectores, extractores, fábricas, staging
    │   ├── Interfaces/                   → IFileReaderRepository<TClass>, IDatabaseReaderRepository<TClass>, IApiReaderRepository<TClass>, IFileFactory
    │   ├── Repositories/                 → CsvFileReaderRepository, JsonFileReaderRepository, SqlDatabaseReaderRepository,
    │   │                                   ApiReaderRepository, SqlStagingRepository, FileStagingRepository
    │   ├── Factory/                      → FileFactory (crea lectores), ExtractorFactory (crea extractores desde appsettings.json)
    │   ├── Extractors/                   → ExtractorBase, CsvExtractor, DatabaseExtractor, ApiExtractor
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
2. **Ejecutar.** Abre `OpinionesClientes.Extraccion.sln` en Visual Studio, elige el perfil de inicio **"API redes sociales + Worker de extracción"** en la barra de herramientas (junto al botón de iniciar) y presiona **F5**. Ese perfil está en `OpinionesClientes.Extraccion.slnLaunch` e inicia la API y el Worker a la vez.
   O todo desde PowerShell (crea las bases, compila y ejecuta): `.\scripts\demo-completa.bat`
3. **API key.** En desarrollo, `launchSettings.json` pasa a la API y al Worker la variable de entorno `SocialApi__ApiKey` con una clave local de prueba, válida solo para la API simulada. En un servidor real la clave se define como variable de entorno o con User Secrets (`scripts\configurar-secretos.ps1`); `appsettings.json` nunca la contiene.
4. **Validar.** En SSMS ejecuta `database/02_ConsultasValidacionStaging.sql`.

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
