# Configuración de Antiforgery para SRAUMOAR

## Problema Identificado

El error 400 en la página de cobro de aranceles se debe a un problema con la validación del token antiforgery:

```
Antiforgery token validation failed. The antiforgery token could not be decrypted.
System.FormatException: Malformed input: 397 is an invalid input length.
```

## Causas del Problema

### 1. **Token Antiforgery Corrupto o Expirado**

- El token se corrompe durante la transmisión
- El token expira antes de ser enviado
- Problemas de codificación en el navegador

### 2. **Configuración Incorrecta**

- Falta de configuración específica en Program.cs
- Configuración de cookies incompatible
- Problemas de SameSite y SecurePolicy

### 3. **Problemas de Navegación**

- Navegación entre páginas sin refrescar el token
- Uso de botones de navegación del navegador
- Problemas con el historial del navegador

## Soluciones Implementadas

### 1. **Configuración en Program.cs**

```csharp
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.HeaderName = "X-XSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
});
```

### 2. **Token en la Vista**

```html
<form method="post" id="form-cobro">
  @Html.AntiForgeryToken()
  <!-- resto del formulario -->
</form>
```

### 3. **Configuración en appsettings.json**

```json
"Antiforgery": {
  "Cookie": {
    "Name": "XSRF-TOKEN",
    "HttpOnly": false,
    "SecurePolicy": "SameAsRequest",
    "SameSite": "Lax"
  },
  "HeaderName": "X-XSRF-TOKEN"
}
```

### 4. **Manejo de Errores en el Controlador**

```csharp
public async Task<IActionResult> OnPostAsync()
{
    try
    {
        // Verificar errores de antiforgery
        if (!ModelState.IsValid)
        {
            var antiforgeryErrors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Where(e => e.ErrorMessage.Contains("antiforgery") || e.ErrorMessage.Contains("token"))
                .ToList();

            if (antiforgeryErrors.Any())
            {
                ModelState.AddModelError(string.Empty, "Error de validación de seguridad. Por favor, recarga la página e intenta nuevamente.");
                return Page();
            }
        }
        // ... resto de la lógica
    }
    catch (Exception ex)
    {
        // manejo de errores
    }
}
```

### 5. **JavaScript para Manejo Automático**

```javascript
// Refrescar token automáticamente
function refreshAntiforgeryToken() {
  fetch("/aranceles/Cobrar?id=@alumno?.AlumnoId", {
    method: "GET",
    headers: {
      "X-Requested-With": "XMLHttpRequest",
    },
  })
    .then((response) => response.text())
    .then((html) => {
      // Extraer y actualizar el token
      const parser = new DOMParser();
      const doc = parser.parseFromString(html, "text/html");
      const newToken = doc.querySelector(
        'input[name="__RequestVerificationToken"]'
      );

      if (newToken) {
        const currentToken = document.querySelector(
          'input[name="__RequestVerificationToken"]'
        );
        if (currentToken) {
          currentToken.value = newToken.value;
        }
      }
    });
}

// Refrescar cada 30 minutos
setInterval(refreshAntiforgeryToken, 30 * 60 * 1000);
```

## Pasos para Probar

### 1. **Reiniciar la Aplicación**

- Detener la aplicación
- Limpiar cookies del navegador
- Reiniciar la aplicación

### 2. **Probar con el Alumno Problemático**

- Navegar a `/aranceles/Cobrar?id=35`
- Verificar que no hay error 400
- Verificar que el token antiforgery está presente

### 3. **Verificar en la Consola del Navegador**

- Abrir herramientas de desarrollador
- Verificar que no hay errores de JavaScript
- Verificar que el token se está refrescando

### 4. **Verificar en los Logs del Servidor**

- Buscar mensajes de antiforgery
- Verificar que no hay errores de validación
- Confirmar que las solicitudes POST funcionan

## Solución de Problemas

### **Si el Problema Persiste:**

1. **Limpiar Cookies del Navegador**

   - Eliminar todas las cookies del sitio
   - Reiniciar el navegador

2. **Verificar Configuración de HTTPS**

   - Asegurar que la configuración de cookies sea compatible
   - Verificar políticas de SameSite

3. **Verificar Configuración de Proxy/Load Balancer**

   - Algunos proxies pueden interferir con las cookies
   - Verificar headers de seguridad

4. **Verificar Configuración de IIS/Apache**
   - Configuración de cookies a nivel de servidor
   - Headers de seguridad adicionales

## Monitoreo

### **Logs a Observar:**

- `Antiforgery token validation failed`
- `The antiforgery token could not be decrypted`
- `Malformed input: X is an invalid input length`

### **Métricas a Monitorear:**

- Tasa de errores 400 por página
- Tiempo de respuesta de las páginas
- Frecuencia de refresco de tokens

## Contacto

Si el problema persiste después de implementar estas soluciones:

1. Revisar logs del servidor
2. Verificar configuración del navegador
3. Probar en diferentes navegadores
4. Verificar configuración de red/proxy

