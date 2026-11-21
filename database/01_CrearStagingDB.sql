/* =====================================================================
   Script 01 - Base de datos STAGING (área de preparación del ETL)
   Proyecto: Sistema de Análisis de Opiniones de Clientes
   Actividad 1 - Arquitectura y proceso de extracción (E del ETL)

   El Worker Service deja aquí los datos tal como vienen de cada fuente
   (CSV, base de datos del sitio web y API de redes sociales), sin
   transformar. Las tablas no tienen claves foráneas a propósito:
   staging recibe datos "crudos"; la limpieza y la integridad se
   aplican en la Transformación, antes de cargar OpinionesClientesDB
   y el data warehouse OpinionesDW.
   ===================================================================== */
USE master;
GO

IF DB_ID(N'OpinionesStagingDB') IS NOT NULL
BEGIN
    ALTER DATABASE OpinionesStagingDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE OpinionesStagingDB;
END
GO

CREATE DATABASE OpinionesStagingDB;
GO
-- Staging se puede reconstruir desde las fuentes: no necesita log completo
ALTER DATABASE OpinionesStagingDB SET RECOVERY SIMPLE;
GO

USE OpinionesStagingDB;
GO

CREATE SCHEMA stg AUTHORIZATION dbo;
GO

/* ---------------------------------------------------------------------
   Columnas de trazabilidad presentes en todas las tablas staging:
   IdLote           -> ejecución del Worker que trajo el registro
   FechaExtraccion  -> momento en que se extrajo
   FuenteExtraccion -> nombre de la fuente en appsettings.json
   Origen           -> archivo, base de datos o URL de donde salió
   --------------------------------------------------------------------- */

-- Fuente CSV: encuestas internas (surveys_part1.csv)
CREATE TABLE stg.Encuestas
(
    IdStaging            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_Encuestas PRIMARY KEY,
    IdOpinion            NVARCHAR(20)   NOT NULL,
    IdCliente            NVARCHAR(20)   NULL,
    IdProducto           NVARCHAR(20)   NULL,
    Fecha                DATE           NULL,
    Comentario           NVARCHAR(1000) NULL,
    Clasificacion        NVARCHAR(50)   NULL,
    PuntajeSatisfaccion  INT            NULL,
    Fuente               NVARCHAR(50)   NULL,
    IdLote               UNIQUEIDENTIFIER NOT NULL,
    FechaExtraccion      DATETIME2(3)   NOT NULL,
    FuenteExtraccion     NVARCHAR(100)  NOT NULL,
    Origen               NVARCHAR(300)  NOT NULL
);

-- Fuente Base de datos: reseñas del sitio web (SitioWebDB.dbo.Resenas)
CREATE TABLE stg.ResenasWeb
(
    IdStaging            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_ResenasWeb PRIMARY KEY,
    IdReview             NVARCHAR(20)   NOT NULL,
    IdCliente            NVARCHAR(20)   NULL,
    IdProducto           NVARCHAR(20)   NULL,
    Fecha                DATE           NULL,
    Comentario           NVARCHAR(1000) NULL,
    Rating               INT            NULL,
    IdLote               UNIQUEIDENTIFIER NOT NULL,
    FechaExtraccion      DATETIME2(3)   NOT NULL,
    FuenteExtraccion     NVARCHAR(100)  NOT NULL,
    Origen               NVARCHAR(300)  NOT NULL
);

-- Fuente API REST: comentarios de redes sociales
CREATE TABLE stg.ComentariosSociales
(
    IdStaging            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_ComentariosSociales PRIMARY KEY,
    IdComment            NVARCHAR(20)   NOT NULL,
    IdCliente            NVARCHAR(20)   NULL,
    IdProducto           NVARCHAR(20)   NULL,
    Fuente               NVARCHAR(50)   NULL,
    Fecha                DATE           NULL,
    Comentario           NVARCHAR(1000) NULL,
    IdLote               UNIQUEIDENTIFIER NOT NULL,
    FechaExtraccion      DATETIME2(3)   NOT NULL,
    FuenteExtraccion     NVARCHAR(100)  NOT NULL,
    Origen               NVARCHAR(300)  NOT NULL
);

-- Maestros en CSV: clientes, productos y registro de fuentes
CREATE TABLE stg.Clientes
(
    IdStaging            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_Clientes PRIMARY KEY,
    IdCliente            NVARCHAR(20)   NOT NULL,
    Nombre               NVARCHAR(200)  NULL,
    Email                NVARCHAR(200)  NULL,
    IdLote               UNIQUEIDENTIFIER NOT NULL,
    FechaExtraccion      DATETIME2(3)   NOT NULL,
    FuenteExtraccion     NVARCHAR(100)  NOT NULL,
    Origen               NVARCHAR(300)  NOT NULL
);

CREATE TABLE stg.Productos
(
    IdStaging            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_Productos PRIMARY KEY,
    IdProducto           NVARCHAR(20)   NOT NULL,
    Nombre               NVARCHAR(200)  NULL,
    Categoria            NVARCHAR(100)  NULL,
    IdLote               UNIQUEIDENTIFIER NOT NULL,
    FechaExtraccion      DATETIME2(3)   NOT NULL,
    FuenteExtraccion     NVARCHAR(100)  NOT NULL,
    Origen               NVARCHAR(300)  NOT NULL
);

CREATE TABLE stg.FuentesDatos
(
    IdStaging            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_FuentesDatos PRIMARY KEY,
    IdFuente             NVARCHAR(20)   NOT NULL,
    TipoFuente           NVARCHAR(50)   NULL,
    FechaCarga           DATE           NULL,
    IdLote               UNIQUEIDENTIFIER NOT NULL,
    FechaExtraccion      DATETIME2(3)   NOT NULL,
    FuenteExtraccion     NVARCHAR(100)  NOT NULL,
    Origen               NVARCHAR(300)  NOT NULL
);
GO

-- Bitácora: una fila por fuente en cada ejecución (no se vacía, guarda el historial)
CREATE TABLE stg.LogExtraccion
(
    IdLog                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_stg_LogExtraccion PRIMARY KEY,
    IdLote               UNIQUEIDENTIFIER NOT NULL,
    Fuente               NVARCHAR(100)  NOT NULL,
    TipoFuente           NVARCHAR(20)   NOT NULL,
    Entidad              NVARCHAR(50)   NOT NULL,
    TablaStaging         NVARCHAR(128)  NOT NULL,
    Origen               NVARCHAR(300)  NOT NULL,
    RegistrosExtraidos   INT            NOT NULL,
    RegistrosRechazados  INT            NOT NULL,
    RegistrosGuardados   INT            NOT NULL,
    FechaInicio          DATETIME2(3)   NOT NULL,
    FechaFin             DATETIME2(3)   NOT NULL,
    DuracionMs           BIGINT         NOT NULL,
    Estado               NVARCHAR(10)   NOT NULL CONSTRAINT CK_stg_LogExtraccion_Estado CHECK (Estado IN (N'OK', N'ERROR')),
    MensajeError         NVARCHAR(1000) NULL
);

CREATE INDEX IX_stg_LogExtraccion_Lote ON stg.LogExtraccion (IdLote, FechaInicio);
GO

/* Vista: resultado de la última ejecución del Worker */
CREATE VIEW stg.vw_UltimoLote
AS
SELECT  l.Fuente, l.TipoFuente, l.TablaStaging, l.RegistrosExtraidos, l.RegistrosRechazados,
        l.RegistrosGuardados, l.DuracionMs, l.Estado, l.MensajeError, l.FechaInicio, l.IdLote
FROM    stg.LogExtraccion AS l
WHERE   l.IdLote = (SELECT TOP (1) IdLote FROM stg.LogExtraccion ORDER BY FechaInicio DESC);
GO

PRINT N'OpinionesStagingDB creada: 6 tablas staging + stg.LogExtraccion + vista stg.vw_UltimoLote';
GO
