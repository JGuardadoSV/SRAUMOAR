# Correcciones de seguimiento de facturación electrónica — 2026-11-02

## Alcance y restricciones

Se corrigieron los hallazgos de la auditoría estática de los commits 0487ba4 a 9f60674. No se inició la aplicación, no se abrió ninguna base de datos, no se ejecutaron migraciones, no se usaron certificados ni credenciales reales y no se transmitieron ni consultaron documentos reales a Hacienda. Las consultas HTTP de pruebas utilizan un handler simulado con el dominio transporte.invalid. Se consultó únicamente documentación pública de Hacienda para obtener los esquemas.

El cambio previo del usuario en appsettings.json se conservó. No hay cambios de modelos persistentes ni migraciones. No se realizó commit ni push.

## Cambios

- Reportes: las ventas netas incluyen solo documentos aceptados; las notas de crédito restan. Compras, donaciones y ERET permanecen fuera del total de ventas. Los anexos conservan su clasificación propia. Se indica el ambiente y se conserva el filtro al exportar y paginar. Producción es el valor inicial explícito.
- Históricos: cuando falta AmbienteFiscal se lee el ambiente del JSON. Los documentos sin ambiente recuperable no se atribuyen automáticamente a producción. La paginación de documentos actuales se mantiene en SQL.
- Consulta y conciliación: acción POST protegida por antiforgery y los roles de la página. Primero recupera evidencia local aceptada. Si corresponde, consulta la identidad y ambiente originales mediante el endpoint de consulta directo o el de la API externa usado por FacturaME. No firma ni emite. Solo una respuesta PROCESADO con sello utilizable, código y ambiente coincidentes acredita aceptación. Errores HTTP, respuestas incompletas y RECHAZADO de la consulta no liberan reservas.
- Concurrencia: una transmisión reciente en curso no se consulta; una operación sin historial no se considera enviada. Las escrituras de conciliación son condicionales y se confirman junto con el historial. La recuperación de emisión no reabre una factura en invalidación ni degrada una aceptación concurrente.
- Invalidaciones: si el evento aceptado quedó guardado pero la factura no, se completa la anulación desde el mismo JSON, identidad, ambiente y sello locales. Una invalidación incierta sin evidencia aceptada permanece reservada: no se infiere su aceptación desde la consulta de la emisión original ni se retransmite.
- Notas: GUID explícito de operación, huella del contenido y exclusión por operación. Un doble POST conserva documento, JSON e identidad; dos operaciones deliberadamente distintas pueden tener el mismo monto y motivo. Alterar un GUID ya reservado con otros datos se rechaza. Un rechazo fiscal se conserva; el formulario permite iniciar otra operación corregida. La fecha local se toma del JSON fiscal.
- Edición manual: detalle, resumen y relaciones financieras quedan protegidos para no desajustar totales ni cobros. Los cambios permitidos se validan contra el esquema y se actualiza el correo del receptor.
- PDF: datos de identificación, NRC, actividad, dirección, teléfono, detalle con bases y descuentos, retenciones, tributos, sello y QR/enlace con ambiente y fecha originales. Se conserva soporte de ERET e invalidación. El botón PDF del listado ahora utiliza su handler correcto. Se agregó QRCoder 1.6.0.

## Esquemas oficiales y nota de débito

La auditoría inicial comprobó igualdad con FacturaME, pero la descarga oficial actual contiene correcciones posteriores a esa copia de referencia. Se incorporaron sin inventar campos. La nota de débito versión 4 ya tiene propiedades tributos con nombre correcto en cuerpo y resumen y se puede generar; se mantiene el rechazo preventivo de una copia corrupta.

Fuente: [Información técnica y funcional de Hacienda](https://factura.gob.sv/informacion-tecnica-y-funcional/), paquete Json Schemas, identificador de descarga 4399, consultado el 2026-11-02. [Descarga oficial](https://factura.gob.sv/download/3-json-schemas-anexo-al-manual-tecnico-para-la-integracion-tecnologica-del-sistema-de-transmision-contiene-los-json-schemas-por-cada-tipo-de-dte-y-cada-tipo-de-evento-para-su-desarrollo-informatic/?wpdmdl=4399).

Los archivos siguientes coinciden en contenido JSON con el paquete oficial consultado. Se registran sus hashes locales SHA-256:

| Archivo | SHA-256 |
| --- | --- |
| fe-eret-v1.json | B845E98CA8ACAA4E5829ACDC36CB046D97EC87E71C823252895BDF907FDAFB35 |
| fe-cd-v2.json | BD7BE594F5AFB7A426DD444B886FF644D3342C5270E1DA50EEF7AB91EDAEC81F |
| fe-f-v2.json | 2887FF3CCF5594EB1F49DCEC1CE8BBA134A86462F900069DE1365D18FF997AFF |
| fe-fse-v2.json | 21B20AE93F85795DB3731EE853504EDCA9998F7F340DEFD0B6745659840BC75D |
| fe-fex-v3.json | 9539E63B4AA80272A835E1E5E28435B466F17EF1FF8C768F81BE156844D57977 |
| invalidacion-schema-v3.json | B57F100FC446B3AEC081AFB6EB4ED2C277E46B6C06FCF3F8A0A28220A7CB4096 |
| contingencia-schema-v4.json | 03FC357397DC7AAA4A8B44F887497DD1EAD3A04EB71EC043BB2C0CDA485BB149 |
| fe-ccf-v4.json | 2C2DB8B3F6144BDD0D128EC2BABECD38F277F8493458A06CF1B2BE7B816FBA32 |
| fe-nc-v4.json | F663902030A9EF28DDB8CEF7D1E93FA286C1852465175E25D524E3FEDB5419D2 |
| fe-nd-v4.json | 3B4FFDD4DDED315217C365325135D3AEDE0A67B84BD051C932698C5E5E0C3CA4 |

## Validación y límites

Se compilan aplicación, vistas Razor y pruebas sin iniciar el servidor. Las pruebas verifican ventas netas, ambientes históricos, protección financiera, identidad de notas, consultas simuladas, recuperación exacta de invalidaciones, notas 05/06 en ambos ambientes y PDF de una y múltiples páginas con datos, enlace e imagen QR. Resultado final: **281 pruebas aprobadas, 0 fallidas y 0 omitidas**. `git diff --check` sin errores.

La exclusión y atomicidad en SQL Server, las credenciales y certificados instalados, las consultas contra servicios fiscales y la entrega SMTP requieren una futura validación de integración autorizada. Estas pruebas no certifican aceptación fiscal real. Los avisos preexistentes de NuGet sobre MailKit e ImageSharp no se resolvieron dentro de este cambio.
