-- Script para agregar la columna EsEspecializacion a la tabla grupos
-- Ejecutar este script directamente en SQL Server Management Studio o en tu herramienta de base de datos

ALTER TABLE grupos
ADD EsEspecializacion bit NOT NULL DEFAULT 0;

-- Verificar que la columna se creó correctamente
-- SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'grupos' AND COLUMN_NAME = 'EsEspecializacion';

