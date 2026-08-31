# Correcciones de auditoría de facturación electrónica

## Alcance y restricciones

Revisión sobre las integraciones recientes de facturación electrónica y ERET. Trabajo exclusivamente en código, esquemas locales y pruebas aisladas. No se inició la aplicación, no se utilizó EF CLI, no se abrió una base de datos y no se transmitieron documentos a Hacienda ni se usaron certificados o credenciales reales. Los clientes HTTP de pruebas utilizan respuestas simuladas; los certificados de pruebas se generan temporalmente y no pertenecen al contribuyente.

No hay cambios de modelo persistente, migraciones ni snapshot. Las compatibilidades históricas se resuelven en código. El commit es local; no incluye push.

## Correcciones

- Borradores nuevos 01/03/14/15 adaptados a las estructuras de referencia 2/4/2/2; validación completa con esquemas incrustados. Los documentos históricos no se transforman durante un reintento.
- Notas de crédito: emisor sin campos prohibidos, IVA proporcional sin doble escalado, impuestos de línea y neto conservados; datos del receptor histórico completados mediante selección explícita de catálogos, sin cambiar el JSON original.
- Ambientes y versiones tomados del documento reservado. Contrato del firmador externo corregido. Errores antes del envío quedan preparados; incertidumbre posterior no se retransmite automáticamente. Renovación de token limitada a un 401 explícito.
- Certificados: validación estructural, clave privada y NIT antes de sustitución atómica. No se trunca un certificado válido por una carga inválida. Clientes fiscales sin redirecciones automáticas.
- Compatibilidad de facturas históricas con sello y estado inicial Preparado, sin migración de datos.
- Reservas de crédito y ERET verificadas dentro de la transacción; identidad explícita de operación ERET para distinguir un reintento de otro retorno idéntico. Seguro/flete de exportación leídos con sus nombres originales.
- Resultados no aceptados conservados y mensajes de éxito condicionados a aceptación. Factura y cobro de aranceles guardados juntos antes de encolar correo; importes de cobro y JSON comparten la fuente autorizada.
- PDF con importes exentos/no sujetos/compra. Recuperación de correos Procesando con lease vencido y exclusión compartida con reenvío manual.
- Historial de pagos restringido a roles autorizados y sin borrado directo de cobros fiscales.
- Reproceso conserva JSON, identidad y ambiente; edición manual bloqueada para documentos con intentos fiscales previos.

## Bloqueo de referencia: nota de débito 06 versión 4

La copia `FacturaME/FacturaME/svfe-json-schemas nuevos/v4/fe-nd-v4.json` tiene propiedades obligatorias cuyo nombre es la cadena vacía en el cuerpo y el resumen, en lugar de un nombre fiscal utilizable. Se conserva la copia sin alteraciones y se bloquea la emisión con un mensaje explícito. No se inventó un esquema ni se agregaron propiedades vacías al JSON fiscal. Se requiere una copia oficial corregida para habilitar la nota de débito versión 4.

## Verificación y límites

Resultado final: **234 pruebas aprobadas, 0 fallidas y 0 omitidas**. Pruebas unitarias sobre esquemas, cálculos, reglas de reserva, idempotencia, selección de catálogos, transporte simulado, certificados sintéticos, permisos, PDF y recuperación de cola. Compilación de solución y vistas Razor sin iniciar el servidor. Verificación de sintaxis del buscador/modal y `git diff --check` sin errores.

La revisión cruzada detectó y permitió corregir también la sobrescritura de una edición concurrente durante la reclamación de un envío, y las diferencias de lectura decimal según la cultura del servidor.

La exclusión y atomicidad SQL, los catálogos realmente importados, las credenciales de cada ambiente y la entrega SMTP requieren una futura validación de integración expresamente autorizada. Esta revisión no certifica aceptación fiscal real. SMTP no ofrece exactamente-una-vez: una caída después de entregar y antes de persistir puede originar un reenvío al recuperar la cola.
