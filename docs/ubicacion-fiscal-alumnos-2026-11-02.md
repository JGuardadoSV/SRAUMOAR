# Ubicación fiscal de alumnos y punto de venta

Fecha: 2026-11-02.

## Comportamiento

- Al entrar al cobro de un estudiante o a su ficha, se comprueba su ubicación fiscal. Si está incompleta o no corresponde a la jerarquía, se abre el modal antes de continuar. El formulario permite elegir Departamento → Municipio → Distrito y actualizar la dirección. Queda disponible un botón para editar una ubicación ya completa.
- Registrar y editar alumnos exige los tres códigos y una dirección de hasta 200 caracteres. La facturación toma los códigos guardados del alumno; no acepta otra ubicación enviada en campos ocultos del cobro.
- El punto de venta usa los mismos selectores dependientes. Cambiar departamento limpia municipio y distrito; cambiar municipio limpia distrito. La emisión también verifica la combinación en el servidor. Se guardan códigos de dos caracteres en el JSON, no IDs de filas.
- El PDF resuelve nombres y muestra códigos para la combinación completa de emisor y receptor. Una dirección histórica sin distrito conserva sus códigos originales y no se interpreta como un municipio nuevo por coincidencia numérica.

## Fuente y modelo

Fuente territorial incorporada: subconjunto CAT-012, CAT-013 y CAT-008 de FacturaME/FacturaME/Data/Catalogos/catalogos-dte.json. Versión 1.1, Excel declarado Catálogos - Facturación Electrónica (3).xlsx, SHA-256 declarado e86d7edc503d876564cd2bf9b251fb100f838199330c0c513048f4075669b2c6. Se conservaron los nombres y ámbitos: 14 departamentos, 44 municipios y 262 distritos nacionales, más las opciones para extranjeros. Los 262 distritos se contrastaron con territorio-asamblea.json de SRAUMOAR sin discrepancias.

La fuente es un recurso de la aplicación: no necesita ejecutar un seeder ni consultar una base de datos para poblar los selectores o mapear los PDF. El modelo anterior Departamento → Distrito → Municipio se conserva para sus usos históricos. No se deducen los códigos nuevos desde MunicipioId.

Se añaden a Alumno tres campos opcionales en almacenamiento: CodigoDepartamentoFiscal, CodigoMunicipioFiscal y CodigoDistritoFiscal. La validación de registro, actualización y emisión los exige. La nulabilidad permite conservar alumnos existentes pendientes de confirmar ubicación.

## Actualización de estructura pendiente

La migración 20261102220000_UbicacionFiscalAlumno agrega exclusivamente tres columnas nvarchar(2) NULL. Incluye Designer y snapshot. No rellena, convierte, elimina ni renombra datos existentes.

**La migración debe aplicarse antes de ejecutar esta versión contra una base de datos. No se aplicó en esta sesión.** Se respetó la restricción de no conectar bases de datos existentes; tampoco se inició la aplicación ni se transmitieron DTE.

El endpoint de actualización del modal está restringido a Administrador, Administracion y Contabilidad y exige antiforgery. Solo modifica dirección y los tres códigos; verifica una huella y los valores originales antes de escribir para detectar una edición concurrente. No expone fotos ni el resto del perfil.

## Verificación

Resultado: **300 pruebas .NET aprobadas y 4 pruebas JavaScript aprobadas**, sin fallos. Aplicación y vistas Razor compiladas. `git diff --check` sin errores. Pruebas aisladas cubren el catálogo completo, combinaciones inválidas, códigos repetidos, perfil histórico incompleto, rechazo de entradas sin abrir conexiones, roles/antiforgery, PDF con nombres/códigos y generación offline del SQL de migración. Un interceptor falla si alguna prueba intenta abrir una conexión.

Las pruebas de interfaz ejecutan el JavaScript con eventos y respuestas simuladas: cambios de padres, restauración de los 263 distritos, apertura/bloqueo del modal, guardado con antiforgery y fallo de carga. No reemplazan una prueba visual e integración completa después de aplicar la migración en un entorno autorizado.
