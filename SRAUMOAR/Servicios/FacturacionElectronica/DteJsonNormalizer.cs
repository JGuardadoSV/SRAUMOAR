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
                NormalizarToken(propiedad.Value);
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
}
