# Guía de Debug para la Página de Cobro de Aranceles

## Problema Identificado

Algunos alumnos generan error 400 (Bad Request) cuando se intenta acceder a la página de cobro de aranceles, mientras que otros funcionan correctamente.

## Mejoras Implementadas

### 1. Manejo de Errores Mejorado

- Se agregó try-catch en todos los métodos críticos
- Se implementó logging detallado para debugging
- Se agregaron validaciones de parámetros

### 2. Validaciones Adicionales

- **ValidarAlumno**: Verifica que el alumno existe y está activo
- **VerificarIntegridadRelaciones**: Valida las relaciones entre entidades
- **GenerarReporteDebug**: Crea un reporte detallado del estado del sistema

### 3. Logging Configurado

- Se configuró logging detallado en `appsettings.Development.json`
- Se agregaron logs en puntos críticos del código
- Se incluye información del usuario y contexto

### 4. Interfaz de Usuario Mejorada

- Se muestran errores de validación claros
- Se agregó botón de debug para desarrolladores
- Se incluye información técnica cuando es necesario

## Cómo Usar el Debug

### 1. Verificar Logs del Servidor

Los logs detallados se escriben en la consola de debug. Busca mensajes que contengan:

- "Error en OnGetAsync"
- "ValidarAlumno"
- "VerificarIntegridadRelaciones"
- "AlumnoHaPagado"

### 2. Usar el Botón de Debug

Cuando ocurra un error:

1. Haz clic en "Mostrar información de debug"
2. Revisa la información mostrada
3. Verifica la consola del navegador

### 3. Revisar el Reporte de Debug

El sistema genera automáticamente un reporte que incluye:

- Estado del alumno
- Ciclo activo
- Aranceles disponibles
- Cobros existentes

## Posibles Causas del Error 400

### 1. Alumno Inexistente o Inactivo

- El ID del alumno no existe en la base de datos
- El alumno está marcado como inactivo (Estado != 1)

### 2. Problemas de Relaciones

- Falta de ciclo activo
- Aranceles sin ciclo asignado
- Problemas en las relaciones de base de datos

### 3. Problemas de Permisos

- El usuario no tiene permisos para ver el alumno
- Problemas de autenticación

### 4. Datos Corruptos

- Aranceles con datos inconsistentes
- Problemas en las fechas o costos

## Pasos para Resolver

### 1. Verificar el Alumno

```sql
SELECT * FROM Alumno WHERE AlumnoId = [ID_PROBLEMATICO]
```

### 2. Verificar el Ciclo Activo

```sql
SELECT * FROM Ciclos WHERE Activo = 1
```

### 3. Verificar Aranceles

```sql
SELECT * FROM Aranceles WHERE CicloId = [CICLO_ACTIVO_ID]
```

### 4. Verificar Permisos

- Confirmar que el usuario tiene rol "Administrador" o "Administracion"
- Verificar que el alumno pertenece a una carrera/facultad accesible

## Archivos Modificados

1. **Pages/aranceles/Cobrar.cshtml.cs** - Lógica de backend mejorada
2. **Pages/aranceles/Cobrar.cshtml** - Interfaz de usuario mejorada
3. **appsettings.json** - Configuración de logging
4. **appsettings.Development.json** - Logging detallado para desarrollo

## Próximos Pasos

1. Probar con el alumno problemático (ID: 35)
2. Revisar los logs del servidor
3. Verificar la base de datos para inconsistencias
4. Implementar monitoreo continuo si es necesario

## Contacto

Si el problema persiste después de revisar esta información, proporciona:

- ID del alumno problemático
- Logs del servidor
- Reporte de debug generado
- Pasos para reproducir el error

