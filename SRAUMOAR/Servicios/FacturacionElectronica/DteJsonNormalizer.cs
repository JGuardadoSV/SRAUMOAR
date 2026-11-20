using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

/// <summary>Normaliza valores de texto fiscales antes de persistirlos o transmitirlos.</summary>
public static class DteJsonNormalizer
{
    public static string Normalizar(string json)
    {
        var token = JToken.Parse(json);
        NormalizarToken(token);
        return token.ToString(Formatting.None);
    }

    private static void NormalizarToken(JToken token)
    {
        if (token is JObject objeto)
        {
            foreach (var propiedad in objeto.Properties().ToList())
            {
                NormalizarToken(propiedad.Value);

                // La integración fiscal de este sistema exige todos los
                // identificadores y teléfonos únicamente con dígitos.
                if (EsCampoSinGuiones(propiedad.Name) && propiedad.Value is JValue valor && valor.Type == JTokenType.String)
                    valor.Value = QuitarGuiones(valor.Value<string>());
            }
        }
        else if (token is JArray arreglo)
        {
            foreach (var elemento in arreglo)
                NormalizarToken(elemento);
        }
        else if (token is JValue valor && valor.Type == JTokenType.String)
        {
            valor.Value = NormalizarTexto(valor.Value<string>());
        }
    }

    private static string? NormalizarTexto(string? valor)
        => valor?.Replace('\u00A0', ' ').Trim();

    private static bool EsCampoSinGuiones(string nombre)
        => nombre.Equals("nit", StringComparison.OrdinalIgnoreCase)
            || nombre.Equals("dui", StringComparison.OrdinalIgnoreCase)
            || nombre.Equals("nrc", StringComparison.OrdinalIgnoreCase)
            || nombre.Equals("telefono", StringComparison.OrdinalIgnoreCase)
            || nombre.Equals("numDocumento", StringComparison.OrdinalIgnoreCase)
            || nombre.Equals("numeroDocResponsable", StringComparison.OrdinalIgnoreCase);

    private static string? QuitarGuiones(string? valor)
        => valor?.Replace("-", string.Empty).Replace(" ", string.Empty);
}
