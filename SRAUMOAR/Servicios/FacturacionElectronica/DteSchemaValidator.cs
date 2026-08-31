using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Json.Schema;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

/// <summary>Validación local; nunca descarga esquemas ni modifica documentos históricos.</summary>
public static class DteSchemaValidator
{
    private static readonly ConcurrentDictionary<string, JsonSchema> Schemas = new();

    public static void Validar(string json, string tipo, int version)
    {
        var archivo = (tipo, version) switch
        {
            ("01", 1) => "fe-fc-v1.json", ("01", 2) => "fe-f-v2.json",
            ("03", 3) => "fe-ccf-v3.json", ("03", 4) => "fe-ccf-v4.json",
            ("05", 3) => "fe-nc-v3.json", ("05", 4) => "fe-nc-v4.json",
            ("06", 3) => "fe-nd-v3.json", ("06", 4) => "fe-nd-v4.json",
            ("11", 1) => "fe-fex-v1.json", ("11", 3) => "fe-fex-v3.json",
            ("14", 1) => "fe-fse-v1.json", ("14", 2) => "fe-fse-v2.json",
            ("15", 1) => "fe-cd-v1.json", ("15", 2) => "fe-cd-v2.json",
            ("18", 1) => "fe-eret-v1.json",
            ("invalidacion", 3) => "invalidacion-schema-v3.json",
            ("contingencia", 4) => "contingencia-schema-v4.json",
            _ => throw new InvalidOperationException($"No existe esquema local para {tipo} versión {version}.")
        };
        var schema = Schemas.GetOrAdd(archivo, nombre =>
        {
            using var stream = typeof(DteSchemaValidator).Assembly.GetManifestResourceStream($"SRAUMOAR.Schemas.{nombre}")
                ?? throw new InvalidOperationException($"No se encontró el esquema {nombre}.");
            using var reader = new StreamReader(stream);
            var texto = reader.ReadToEnd();
            if (nombre == "fe-nd-v4.json" && texto.Contains("\"\":", StringComparison.Ordinal))
                throw new InvalidOperationException("El esquema de referencia fe-nd-v4.json contiene propiedades fiscales con nombre vacío. Instale una copia oficial corregida antes de emitir notas de débito; no se ha transmitido el documento.");
            return JsonSchema.FromText(texto);
        });
        var result = schema.Evaluate(JsonNode.Parse(json), new EvaluationOptions
        {
            EvaluateAs = SpecVersion.Draft7, OutputFormat = OutputFormat.List, RequireFormatValidation = true
        });
        if (!result.IsValid)
        {
            // Se muestran rutas, nunca contenido fiscal ni credenciales.
            var rutas = result.Details?.Where(x => x.HasErrors).Select(x => x.InstanceLocation.ToString()).Distinct().Take(10);
            throw new InvalidOperationException($"El documento no cumple {archivo}. Revise: {string.Join(", ", rutas ?? [])}.");
        }
    }
}
