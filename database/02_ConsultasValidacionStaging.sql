/* =====================================================================
   Script 02 - Consultas de validación del proceso de extracción
   Ejecutar después de correr el Worker Service.
   ===================================================================== */
USE OpinionesStagingDB;
GO

/* 1. Resultado de la última ejecución, fuente por fuente */
SELECT * FROM stg.vw_UltimoLote ORDER BY TipoFuente, Fuente;

/* 2. Registros cargados en cada tabla staging */
SELECT 'stg.Encuestas'           AS Tabla, COUNT(*) AS Registros FROM stg.Encuestas
UNION ALL SELECT 'stg.ResenasWeb',          COUNT(*) FROM stg.ResenasWeb
UNION ALL SELECT 'stg.ComentariosSociales', COUNT(*) FROM stg.ComentariosSociales
UNION ALL SELECT 'stg.Clientes',            COUNT(*) FROM stg.Clientes
UNION ALL SELECT 'stg.Productos',           COUNT(*) FROM stg.Productos
UNION ALL SELECT 'stg.FuentesDatos',        COUNT(*) FROM stg.FuentesDatos;

/* 3. Cuadre origen vs. staging para la fuente de base de datos */
SELECT  (SELECT COUNT(*) FROM SitioWebDB.dbo.Resenas) AS ResenasEnOrigen,
        (SELECT COUNT(*) FROM stg.ResenasWeb)         AS ResenasEnStaging;

/* 4. Trazabilidad: de qué lote, fuente y origen viene cada registro */
SELECT TOP (5) IdComment, IdCliente, Fuente, Fecha, Comentario, IdLote, FechaExtraccion, FuenteExtraccion, Origen
FROM stg.ComentariosSociales
ORDER BY IdComment;

/* 5. Rendimiento: duración de cada fuente en las últimas ejecuciones */
SELECT TOP (30) FechaInicio, Fuente, TipoFuente, RegistrosGuardados, DuracionMs,
       CAST(RegistrosGuardados * 1000.0 / NULLIF(DuracionMs, 0) AS DECIMAL(10,1)) AS RegistrosPorSegundo, Estado
FROM stg.LogExtraccion
ORDER BY FechaInicio DESC;

/* 6. Historial de ejecuciones (un resumen por lote) */
SELECT  IdLote, MIN(FechaInicio) AS Inicio, MAX(FechaFin) AS Fin,
        DATEDIFF(MILLISECOND, MIN(FechaInicio), MAX(FechaFin)) AS DuracionLoteMs,
        SUM(DuracionMs) AS SumaDuracionesMs,
        SUM(RegistrosGuardados) AS Guardados,
        SUM(CASE WHEN Estado = 'ERROR' THEN 1 ELSE 0 END) AS FuentesConError
FROM stg.LogExtraccion
GROUP BY IdLote
ORDER BY Inicio DESC;

/* 7. Errores registrados */
SELECT FechaInicio, Fuente, MensajeError
FROM stg.LogExtraccion
WHERE Estado = 'ERROR'
ORDER BY FechaInicio DESC;
GO
