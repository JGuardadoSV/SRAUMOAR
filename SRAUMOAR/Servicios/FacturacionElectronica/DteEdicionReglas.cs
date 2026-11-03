using Newtonsoft.Json.Linq;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public static class DteEdicionReglas
{
    public static void ValidarSinCambiosFinancieros(JObject original, JObject editado)
    {
        foreach (var seccion in new[] { "cuerpoDocumento", "resumen", "documentoRelacionado", "ventaTercero", "compraTercero" })
            if (!JToken.DeepEquals(original[seccion], editado[seccion]))
                throw new InvalidOperationException("La edición manual no permite cambiar detalle, importes ni relaciones fiscales. Corrija la operación desde su flujo de origen para conservar los totales y el cobro.");
    }
}
