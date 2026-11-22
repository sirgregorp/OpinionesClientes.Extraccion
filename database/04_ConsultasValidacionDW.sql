/* =====================================================================
   Script 04 - Consultas para validar la carga de dimensiones
   Proyecto: Sistema de Análisis de Opiniones de Clientes
   Se ejecuta después de correr el Worker.
   ===================================================================== */
USE OpinionesDW;
GO

-- 1. Resultado de la última carga (bitácora que escribe el Worker)
SELECT Dimension, TablaOrigen, RegistrosLeidos, RegistrosDescartados,
       Insertados, Actualizados, TotalEnDimension, DuracionMs, Estado, FechaInicio
FROM   dbo.vw_UltimaCargaDimensiones
ORDER BY Dimension;

-- 2. Cantidad de filas por dimensión
SELECT 'Dim_Tiempo'        AS Dimension, COUNT(*) AS Filas FROM dbo.Dim_Tiempo
UNION ALL SELECT 'Dim_Cliente',       COUNT(*) FROM dbo.Dim_Cliente
UNION ALL SELECT 'Dim_Producto',      COUNT(*) FROM dbo.Dim_Producto
UNION ALL SELECT 'Dim_Fuente',        COUNT(*) FROM dbo.Dim_Fuente
UNION ALL SELECT 'Dim_Clasificacion', COUNT(*) FROM dbo.Dim_Clasificacion;

-- 3. Muestra de cada dimensión
SELECT TOP (10) * FROM dbo.Dim_Cliente ORDER BY ClienteKey;
SELECT TOP (10) * FROM dbo.Dim_Producto ORDER BY ProductoKey;
SELECT * FROM dbo.Dim_Fuente ORDER BY Canal, NombreFuente;
SELECT * FROM dbo.Dim_Clasificacion ORDER BY ClasificacionKey;
SELECT TOP (10) * FROM dbo.Dim_Tiempo WHERE Anio = 2025 ORDER BY TiempoKey;

-- 4. Productos por categoría
SELECT Categoria, COUNT(*) AS Productos
FROM   dbo.Dim_Producto
GROUP BY Categoria
ORDER BY Productos DESC;

-- 5. Días por año en el calendario
SELECT Anio, COUNT(*) AS Dias, MIN(Fecha) AS Desde, MAX(Fecha) AS Hasta
FROM   dbo.Dim_Tiempo
GROUP BY Anio
ORDER BY Anio;

-- 6. Controles de calidad (todas deberían devolver 0)
SELECT 'Clientes de staging que faltan en Dim_Cliente' AS Control, COUNT(*) AS Cantidad
FROM   OpinionesStagingDB.stg.Clientes AS s
WHERE  TRY_CONVERT(INT, s.IdCliente) IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Dim_Cliente AS d WHERE d.IdClienteOrigen = TRY_CONVERT(INT, s.IdCliente))
UNION ALL
SELECT 'Productos de staging que faltan en Dim_Producto', COUNT(*)
FROM   OpinionesStagingDB.stg.Productos AS s
WHERE  TRY_CONVERT(INT, s.IdProducto) IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Dim_Producto AS d WHERE d.IdProductoOrigen = TRY_CONVERT(INT, s.IdProducto))
UNION ALL
SELECT 'Fechas de opiniones sin fila en Dim_Tiempo', COUNT(*)
FROM  (SELECT Fecha FROM OpinionesStagingDB.stg.Encuestas
       UNION SELECT Fecha FROM OpinionesStagingDB.stg.ResenasWeb
       UNION SELECT Fecha FROM OpinionesStagingDB.stg.ComentariosSociales) AS f
WHERE  f.Fecha IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Dim_Tiempo AS t WHERE t.Fecha = f.Fecha)
UNION ALL
SELECT 'Clientes duplicados por IdClienteOrigen', COUNT(*)
FROM  (SELECT IdClienteOrigen FROM dbo.Dim_Cliente GROUP BY IdClienteOrigen HAVING COUNT(*) > 1) AS d;

-- 7. Historial de cargas
SELECT IdLote, MIN(FechaInicio) AS Fecha, SUM(Insertados) AS Insertados,
       SUM(Actualizados) AS Actualizados, SUM(DuracionMs) AS SumaMs
FROM   dbo.LogCargaDimensiones
GROUP BY IdLote
ORDER BY Fecha DESC;
GO
