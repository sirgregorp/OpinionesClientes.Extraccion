/* =====================================================================
   Script 03 - Base de datos analítica OpinionesDW (modelo estrella)
   Proyecto: Sistema de Análisis de Opiniones de Clientes
   Actividad 2 - Carga de las dimensiones del Data Warehouse

   Es el mismo modelo estrella diseñado en la Actividad 3.1
   (Dim_Tiempo, Dim_Cliente, Dim_Producto, Dim_Fuente, Dim_Clasificacion
   y Fact_Opiniones). El script NO borra nada: si la base o una tabla ya
   existen las deja como están y solo crea lo que falta.
   Agrega además la bitácora dbo.LogCargaDimensiones.

   Las dimensiones las llena el Worker Service (.NET) a partir de las
   tablas staging de OpinionesStagingDB.
   ===================================================================== */
USE master;
GO

IF DB_ID(N'OpinionesDW') IS NULL
    CREATE DATABASE OpinionesDW;
GO

USE OpinionesDW;
GO

/* ------------------------------ Dimensiones ----------------------------- */

IF OBJECT_ID(N'dbo.Dim_Tiempo', N'U') IS NULL
BEGIN
    -- Tiempo: una fila por día (clave inteligente AAAAMMDD)
    CREATE TABLE dbo.Dim_Tiempo
    (
        TiempoKey     INT           NOT NULL,   -- 20250315
        Fecha         DATE          NOT NULL,
        Anio          SMALLINT      NOT NULL,
        Trimestre     TINYINT       NOT NULL,
        Mes           TINYINT       NOT NULL,
        NombreMes     NVARCHAR(15)  NOT NULL,
        AnioMes       CHAR(7)       NOT NULL,   -- 2025-03
        Dia           TINYINT       NOT NULL,
        DiaSemana     TINYINT       NOT NULL,   -- 1 = lunes
        NombreDia     NVARCHAR(15)  NOT NULL,
        EsFinDeSemana BIT           NOT NULL,
        CONSTRAINT PK_Dim_Tiempo PRIMARY KEY (TiempoKey),
        CONSTRAINT UQ_Dim_Tiempo_Fecha UNIQUE (Fecha)
    );
END
GO

IF OBJECT_ID(N'dbo.Dim_Cliente', N'U') IS NULL
BEGIN
    -- Cliente (incluye atributos de segmentación para análisis futuros)
    CREATE TABLE dbo.Dim_Cliente
    (
        ClienteKey       INT IDENTITY(1,1) NOT NULL,
        IdClienteOrigen  INT            NULL,      -- Id del sistema transaccional (NULL = anónimo)
        Nombre           NVARCHAR(150)  NOT NULL,
        Email            NVARCHAR(150)  NULL,
        Pais             NVARCHAR(60)   NOT NULL CONSTRAINT DF_Dim_Cliente_Pais        DEFAULT (N'No especificado'),
        TipoCliente      NVARCHAR(30)   NOT NULL CONSTRAINT DF_Dim_Cliente_TipoCliente DEFAULT (N'No especificado'),
        RangoEdad        NVARCHAR(20)   NOT NULL CONSTRAINT DF_Dim_Cliente_RangoEdad   DEFAULT (N'No especificado'),
        CONSTRAINT PK_Dim_Cliente PRIMARY KEY (ClienteKey)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Dim_Cliente_Origen' AND object_id = OBJECT_ID(N'dbo.Dim_Cliente'))
    CREATE UNIQUE INDEX UX_Dim_Cliente_Origen ON dbo.Dim_Cliente (IdClienteOrigen) WHERE IdClienteOrigen IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.Dim_Producto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dim_Producto
    (
        ProductoKey       INT IDENTITY(1,1) NOT NULL,
        IdProductoOrigen  INT            NOT NULL,
        Nombre            NVARCHAR(150)  NOT NULL,
        Categoria         NVARCHAR(100)  NOT NULL,
        CONSTRAINT PK_Dim_Producto PRIMARY KEY (ProductoKey),
        CONSTRAINT UQ_Dim_Producto_Origen UNIQUE (IdProductoOrigen)
    );
END
GO

IF OBJECT_ID(N'dbo.Dim_Fuente', N'U') IS NULL
BEGIN
    -- Fuente / canal: EncuestaInterna (CSV), SitioWeb (Web), Instagram/Facebook/Twitter (Red Social)
    CREATE TABLE dbo.Dim_Fuente
    (
        FuenteKey    INT IDENTITY(1,1) NOT NULL,
        NombreFuente NVARCHAR(100) NOT NULL,
        TipoFuente   NVARCHAR(50)  NOT NULL,
        Canal        NVARCHAR(30)  NOT NULL,   -- Encuestas | Reseñas web | Redes sociales
        CONSTRAINT PK_Dim_Fuente PRIMARY KEY (FuenteKey),
        CONSTRAINT UQ_Dim_Fuente_Nombre UNIQUE (NombreFuente)
    );
END
GO

IF OBJECT_ID(N'dbo.Dim_Clasificacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Dim_Clasificacion
    (
        ClasificacionKey INT          NOT NULL,
        Nombre           NVARCHAR(20) NOT NULL,
        ValorSentimiento SMALLINT     NOT NULL,   -- 1 positiva, 0 neutra, -1 negativa
        CONSTRAINT PK_Dim_Clasificacion PRIMARY KEY (ClasificacionKey),
        CONSTRAINT UQ_Dim_Clasificacion_Nombre UNIQUE (Nombre)
    );
END
GO

/* --------------------------- Tabla de hechos ---------------------------- */
IF OBJECT_ID(N'dbo.Fact_Opiniones', N'U') IS NULL
BEGIN
    -- Granularidad: una fila por opinión (encuesta, reseña web o comentario social)
    CREATE TABLE dbo.Fact_Opiniones
    (
        OpinionKey           BIGINT IDENTITY(1,1) NOT NULL,
        TiempoKey            INT            NOT NULL,
        ClienteKey           INT            NOT NULL,
        ProductoKey          INT            NOT NULL,
        FuenteKey            INT            NOT NULL,
        ClasificacionKey     INT            NOT NULL,
        -- Dimensiones degeneradas (identifican la opinión original)
        TipoOrigen           NVARCHAR(20)   NOT NULL,
        CodigoOrigen         NVARCHAR(30)   NOT NULL,
        Comentario           NVARCHAR(1000) NULL,      -- para análisis de palabras clave
        -- Medidas
        PuntajeSatisfaccion  TINYINT        NULL,      -- encuestas (1-5)
        Rating               TINYINT        NULL,      -- reseñas web (1-5)
        Calificacion         TINYINT        NULL,      -- puntaje o rating unificado (1-5)
        EsPositiva           TINYINT        NOT NULL,
        EsNeutra             TINYINT        NOT NULL,
        EsNegativa           TINYINT        NOT NULL,
        CantidadComentarios  TINYINT        NOT NULL CONSTRAINT DF_Fact_Opiniones_Cantidad DEFAULT (1),
        FechaCarga           DATETIME2(0)   NOT NULL CONSTRAINT DF_Fact_Opiniones_FechaCarga DEFAULT (SYSDATETIME()),
        CONSTRAINT PK_Fact_Opiniones PRIMARY KEY (OpinionKey),
        CONSTRAINT UQ_Fact_Opiniones_Origen UNIQUE (TipoOrigen, CodigoOrigen),
        CONSTRAINT FK_Fact_Opiniones_Tiempo        FOREIGN KEY (TiempoKey)        REFERENCES dbo.Dim_Tiempo (TiempoKey),
        CONSTRAINT FK_Fact_Opiniones_Cliente       FOREIGN KEY (ClienteKey)       REFERENCES dbo.Dim_Cliente (ClienteKey),
        CONSTRAINT FK_Fact_Opiniones_Producto      FOREIGN KEY (ProductoKey)      REFERENCES dbo.Dim_Producto (ProductoKey),
        CONSTRAINT FK_Fact_Opiniones_Fuente        FOREIGN KEY (FuenteKey)        REFERENCES dbo.Dim_Fuente (FuenteKey),
        CONSTRAINT FK_Fact_Opiniones_Clasificacion FOREIGN KEY (ClasificacionKey) REFERENCES dbo.Dim_Clasificacion (ClasificacionKey),
        CONSTRAINT CK_Fact_Opiniones_Calificacion CHECK (Calificacion IS NULL OR Calificacion BETWEEN 1 AND 5),
        CONSTRAINT CK_Fact_Opiniones_Sentimiento  CHECK (EsPositiva + EsNeutra + EsNegativa = 1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fact_Opiniones_Tiempo' AND object_id = OBJECT_ID(N'dbo.Fact_Opiniones'))
    CREATE INDEX IX_Fact_Opiniones_Tiempo ON dbo.Fact_Opiniones (TiempoKey);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fact_Opiniones_Cliente' AND object_id = OBJECT_ID(N'dbo.Fact_Opiniones'))
    CREATE INDEX IX_Fact_Opiniones_Cliente ON dbo.Fact_Opiniones (ClienteKey);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fact_Opiniones_Producto' AND object_id = OBJECT_ID(N'dbo.Fact_Opiniones'))
    CREATE INDEX IX_Fact_Opiniones_Producto ON dbo.Fact_Opiniones (ProductoKey);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fact_Opiniones_Fuente' AND object_id = OBJECT_ID(N'dbo.Fact_Opiniones'))
    CREATE INDEX IX_Fact_Opiniones_Fuente ON dbo.Fact_Opiniones (FuenteKey);
GO

/* ------------------------- Bitácora de la carga ------------------------- */
IF OBJECT_ID(N'dbo.LogCargaDimensiones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LogCargaDimensiones
    (
        IdLog                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LogCargaDimensiones PRIMARY KEY,
        IdLote               UNIQUEIDENTIFIER NOT NULL,
        Dimension            NVARCHAR(50)   NOT NULL,
        Modo                 NVARCHAR(20)   NOT NULL,      -- Completa | Incremental
        TablaOrigen          NVARCHAR(200)  NOT NULL,
        RegistrosLeidos      INT            NOT NULL,
        RegistrosDescartados INT            NOT NULL,
        Insertados           INT            NOT NULL,
        Actualizados         INT            NOT NULL,
        TotalEnDimension     INT            NOT NULL,
        DuracionMs           BIGINT         NOT NULL,
        Estado               NVARCHAR(10)   NOT NULL CONSTRAINT CK_LogCargaDimensiones_Estado CHECK (Estado IN (N'OK', N'ERROR')),
        MensajeError         NVARCHAR(1000) NULL,
        FechaInicio          DATETIME2(3)   NOT NULL,
        FechaFin             DATETIME2(3)   NOT NULL
    );
END
GO

/* Vista: resultado de la última carga de dimensiones */
CREATE OR ALTER VIEW dbo.vw_UltimaCargaDimensiones
AS
SELECT  l.Dimension, l.Modo, l.TablaOrigen, l.RegistrosLeidos, l.RegistrosDescartados,
        l.Insertados, l.Actualizados, l.TotalEnDimension, l.DuracionMs, l.Estado, l.MensajeError,
        l.FechaInicio, l.IdLote
FROM    dbo.LogCargaDimensiones AS l
WHERE   l.IdLote = (SELECT TOP (1) IdLote FROM dbo.LogCargaDimensiones ORDER BY FechaInicio DESC);
GO

PRINT N'OpinionesDW lista: dimensiones, Fact_Opiniones y bitácora dbo.LogCargaDimensiones.';
GO
