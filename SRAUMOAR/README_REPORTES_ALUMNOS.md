# Reportes de Alumnos - SRAUMOAR

## Descripción

Este módulo permite generar reportes Excel completos de todos los alumnos registrados en el sistema SRAUMOAR, utilizando la librería ClosedXML que ya está configurada en el proyecto.

## Características

### 📊 **Reporte Completo**

- Incluye **12 columnas** con la información esencial de los alumnos
- Formato Excel (.xlsx) profesional y bien estructurado
- Estadísticas automáticas y resúmenes por carrera
- Autoajuste de columnas para mejor visualización

### 🔍 **Reporte Filtrado**

- Filtros por carrera, estado y tipo de ingreso
- Misma calidad de formato que el reporte completo
- Ideal para análisis específicos por departamento o condición
- Incluye las mismas 12 columnas esenciales

## Columnas Disponibles

### **Información Personal**
- Nombres y Apellidos
- DUI
- Género (1=Hombre, 2=Mujer)
- Fecha de nacimiento
- Estado civil (Casado/Soltero)

### **Datos de Contacto**
- Email
- Teléfono primario
- Dirección de residencia

### **Información Académica**
- Carrera
- Carnet (extraído del email si no está asignado)

### **Características del Reporte**
- **Total de columnas:** 12
- Ordenado alfabéticamente por apellidos y nombres
- Estadísticas por carrera
- Formato limpio y fácil de leer

## Endpoints API

### **Reporte Completo**

```
GET /api/reportesalumnos/excel-completo
```

**Autorización:** Administradores, Docentes

### **Reporte Filtrado**

```
GET /api/reportesalumnos/excel-filtrado?carreraId=1&estado=1&ingresoPorEquivalencias=false
```

**Parámetros:**

- `carreraId` (opcional): ID de la carrera
- `estado` (opcional): 1=Activo, 2=Inactivo
- `ingresoPorEquivalencias` (opcional): true/false

### **Estadísticas**

```
GET /api/reportesalumnos/estadisticas
```

## Interfaz Web

### **Acceso**

```
/ReportesAlumnos
```

### **Funcionalidades**

- Botón de descarga directa para reporte completo
- Modal de configuración para filtros
- Vista previa de columnas incluidas
- Información sobre características del reporte

## Instalación y Configuración

### **1. Servicios Registrados**

El servicio ya está registrado en `Program.cs`:

```csharp
builder.Services.AddScoped<ReporteAlumnosService>();
```

### **2. Dependencias**

- ClosedXML (ya instalado)
- Entity Framework Core
- ASP.NET Core

### **3. Permisos**

- Roles requeridos: `Administradores`, `Docentes`
- Configurado en `[Authorize(Roles = "Administradores,Docentes")]`

## Uso

### **Desde la API**

```bash
# Reporte completo
curl -X GET "https://tu-dominio.com/api/reportesalumnos/excel-completo" \
     -H "Authorization: Bearer TU_TOKEN"

# Reporte filtrado
curl -X GET "https://tu-dominio.com/api/reportesalumnos/excel-filtrado?estado=1" \
     -H "Authorization: Bearer TU_TOKEN"
```

### **Desde la Interfaz Web**

1. Navegar a `/ReportesAlumnos`
2. Hacer clic en "Descargar Reporte Completo" para todos los alumnos
3. O usar "Configurar Filtros" para reportes específicos

## Personalización

### **Agregar Nuevas Columnas**

1. Modificar el array `headers` en `ReporteAlumnosService.cs`
2. Agregar la lógica de datos correspondiente
3. Actualizar la documentación

### **Modificar Filtros**

1. Editar el método `GenerarReporteFiltradoAsync`
2. Agregar nuevos parámetros de filtro
3. Actualizar la interfaz web

### **Cambiar Formato**

1. Modificar los estilos en `GenerarReporteCompletoAsync`
2. Ajustar colores, fuentes y bordes
3. Personalizar el resumen y estadísticas

## Solución de Problemas

### **Error: "ClosedXML no está disponible"**

- Verificar que el paquete NuGet esté instalado
- Revisar las referencias en el proyecto

### **Error: "No se puede acceder al contexto de base de datos"**

- Verificar la cadena de conexión
- Comprobar que el servicio esté registrado correctamente

### **Error: "No autorizado"**

- Verificar que el usuario tenga los roles correctos
- Comprobar la configuración de autorización

## Mantenimiento

### **Actualizaciones**

- Revisar regularmente las dependencias de ClosedXML
- Mantener compatibilidad con versiones de .NET
- Actualizar la documentación según cambios

### **Monitoreo**

- Revisar logs de errores en la generación de reportes
- Monitorear el rendimiento con grandes volúmenes de datos
- Verificar que los filtros funcionen correctamente

## Soporte

Para problemas o consultas sobre los reportes de alumnos, contactar al equipo de desarrollo del sistema SRAUMOAR.
