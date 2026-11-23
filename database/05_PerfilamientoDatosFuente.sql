/* =====================================================================
   Script 05 - Exploración y perfilamiento de los datos fuente
   Proyecto: Sistema de Análisis de Opiniones de Clientes

   Basado en el Módulo 6 del curso 20767A (Lesson 2: Exploring Source Data).
   Son las mismas solicitudes de perfil de la tarea Data Profiling de SSIS,
   escritas en T-SQL sobre las tablas staging:
     1. Column Statistics        (mínimo, máximo, promedio)
     2. Column Null Ratio        (porcentaje de nulos)
     3. Column Length Distribution (largo de los textos)
     4. Column Value Distribution  (valores distintos y su frecuencia)
     5. Candidate Key            (¿la columna sirve como llave?)
     6. Value Inclusion          (¿los valores existen en la tabla maestra?)
   ===================================================================== */
USE OpinionesStagingDB;
GO

/* Convierte un código como 'C007' o '7' en el número 7 (igual que el Worker) */
CREATE OR ALTER FUNCTION stg.fn_IdNumerico (@codigo NVARCHAR(20))
RETURNS INT
AS
BEGIN
    DECLARE @i INT = PATINDEX(N'%[0-9]%', @codigo);
    RETURN CASE WHEN @i = 0 THEN NULL ELSE TRY_CONVERT(INT, SUBSTRING(@codigo, @i, 20)) END;
END
GO

-- 1. Column Statistics: rango de fechas y puntajes
SELECT 'stg.Encuestas' AS Tabla, COUNT(*) AS Filas, MIN(Fecha) AS FechaMin, MAX(Fecha) AS FechaMax,
       MIN(PuntajeSatisfaccion) AS PuntajeMin, MAX(PuntajeSatisfaccion) AS PuntajeMax,
       CAST(AVG(CAST(PuntajeSatisfaccion AS DECIMAL(5,2))) AS DECIMAL(5,2)) AS PuntajePromedio
FROM stg.Encuestas
UNION ALL
SELECT 'stg.ResenasWeb', COUNT(*), MIN(Fecha), MAX(Fecha), MIN(Rating), MAX(Rating),
       CAST(AVG(CAST(Rating AS DECIMAL(5,2))) AS DECIMAL(5,2))
FROM stg.ResenasWeb
UNION ALL
SELECT 'stg.ComentariosSociales', COUNT(*), MIN(Fecha), MAX(Fecha), NULL, NULL, NULL
FROM stg.ComentariosSociales;

-- 2. Column Null Ratio: columnas que pueden venir vacías
SELECT 'stg.ComentariosSociales' AS Tabla, 'IdCliente' AS Columna, COUNT(*) AS Filas,
       SUM(CASE WHEN NULLIF(LTRIM(IdCliente), '') IS NULL THEN 1 ELSE 0 END) AS Nulos,
       CAST(100.0 * SUM(CASE WHEN NULLIF(LTRIM(IdCliente), '') IS NULL THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,1)) AS PorcentajeNulos
FROM stg.ComentariosSociales
UNION ALL
SELECT 'stg.Clientes', 'Email', COUNT(*),
       SUM(CASE WHEN NULLIF(LTRIM(Email), '') IS NULL THEN 1 ELSE 0 END),
       CAST(100.0 * SUM(CASE WHEN NULLIF(LTRIM(Email), '') IS NULL THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,1))
FROM stg.Clientes
UNION ALL
SELECT 'stg.Productos', 'Categoria', COUNT(*),
       SUM(CASE WHEN NULLIF(LTRIM(Categoria), '') IS NULL THEN 1 ELSE 0 END),
       CAST(100.0 * SUM(CASE WHEN NULLIF(LTRIM(Categoria), '') IS NULL THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,1))
FROM stg.Productos
UNION ALL
SELECT 'stg.Encuestas', 'Comentario', COUNT(*),
       SUM(CASE WHEN NULLIF(LTRIM(Comentario), '') IS NULL THEN 1 ELSE 0 END),
       CAST(100.0 * SUM(CASE WHEN NULLIF(LTRIM(Comentario), '') IS NULL THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,1))
FROM stg.Encuestas;

-- 3. Column Length Distribution: largo de los textos (para definir los NVARCHAR del DW)
SELECT 'stg.Clientes.Nombre' AS Columna, MIN(LEN(Nombre)) AS LargoMin, MAX(LEN(Nombre)) AS LargoMax FROM stg.Clientes
UNION ALL SELECT 'stg.Clientes.Email', MIN(LEN(Email)), MAX(LEN(Email)) FROM stg.Clientes
UNION ALL SELECT 'stg.Productos.Nombre', MIN(LEN(Nombre)), MAX(LEN(Nombre)) FROM stg.Productos
UNION ALL SELECT 'stg.Encuestas.Comentario', MIN(LEN(Comentario)), MAX(LEN(Comentario)) FROM stg.Encuestas
UNION ALL SELECT 'stg.ComentariosSociales.Comentario', MIN(LEN(Comentario)), MAX(LEN(Comentario)) FROM stg.ComentariosSociales;

-- 4. Column Value Distribution: valores distintos de las columnas que forman dimensiones
SELECT 'Clasificacion (encuestas)' AS Columna, Clasificacion AS Valor, COUNT(*) AS Cantidad FROM stg.Encuestas GROUP BY Clasificacion
UNION ALL SELECT 'Fuente (encuestas)', Fuente, COUNT(*) FROM stg.Encuestas GROUP BY Fuente
UNION ALL SELECT 'Fuente (comentarios)', Fuente, COUNT(*) FROM stg.ComentariosSociales GROUP BY Fuente
UNION ALL SELECT 'Categoria (productos)', Categoria, COUNT(*) FROM stg.Productos GROUP BY Categoria
ORDER BY Columna, Cantidad DESC;

-- 5. Candidate Key: ¿el id del maestro es único? (Duplicados = 0 significa que sirve como llave de negocio)
SELECT 'stg.Clientes.IdCliente' AS Columna, COUNT(*) AS Filas, COUNT(DISTINCT IdCliente) AS Distintos, COUNT(*) - COUNT(DISTINCT IdCliente) AS Duplicados FROM stg.Clientes
UNION ALL SELECT 'stg.Productos.IdProducto', COUNT(*), COUNT(DISTINCT IdProducto), COUNT(*) - COUNT(DISTINCT IdProducto) FROM stg.Productos
UNION ALL SELECT 'stg.Encuestas.IdOpinion', COUNT(*), COUNT(DISTINCT IdOpinion), COUNT(*) - COUNT(DISTINCT IdOpinion) FROM stg.Encuestas;

-- 6. Value Inclusion: ¿los clientes y productos de las opiniones existen en los maestros?
WITH Opiniones AS (
    SELECT 'Encuestas' AS Origen, stg.fn_IdNumerico(IdCliente) AS IdCliente, stg.fn_IdNumerico(IdProducto) AS IdProducto FROM stg.Encuestas
    UNION ALL SELECT 'Reseñas web', stg.fn_IdNumerico(IdCliente), stg.fn_IdNumerico(IdProducto) FROM stg.ResenasWeb
    UNION ALL SELECT 'Comentarios sociales', stg.fn_IdNumerico(IdCliente), stg.fn_IdNumerico(IdProducto) FROM stg.ComentariosSociales
),
Clientes AS (SELECT DISTINCT stg.fn_IdNumerico(IdCliente) AS Id FROM stg.Clientes),
Productos AS (SELECT DISTINCT stg.fn_IdNumerico(IdProducto) AS Id FROM stg.Productos)
SELECT o.Origen,
       COUNT(*) AS Opiniones,
       SUM(CASE WHEN o.IdCliente IS NULL THEN 1 ELSE 0 END) AS SinCliente,
       SUM(CASE WHEN o.IdCliente IS NOT NULL AND c.Id IS NULL THEN 1 ELSE 0 END) AS ClienteNoExiste,
       SUM(CASE WHEN p.Id IS NULL THEN 1 ELSE 0 END) AS ProductoNoExiste,
       CAST(100.0 * SUM(CASE WHEN c.Id IS NOT NULL OR o.IdCliente IS NULL THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,1)) AS PctClienteValido,
       CAST(100.0 * SUM(CASE WHEN p.Id IS NOT NULL THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,1)) AS PctProductoValido
FROM Opiniones AS o
LEFT JOIN Clientes  AS c ON c.Id = o.IdCliente
LEFT JOIN Productos AS p ON p.Id = o.IdProducto
GROUP BY o.Origen
ORDER BY o.Origen;
GO
