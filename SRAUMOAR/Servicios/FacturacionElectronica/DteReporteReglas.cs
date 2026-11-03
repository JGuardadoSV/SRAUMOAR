using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.Colecturia;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public static class DteReporteReglas
{
    public static string? Ambiente(Factura factura)
    {
        if (factura.AmbienteFiscal is "00" or "01") return factura.AmbienteFiscal;
        try { return JObject.Parse(factura.JsonDte ?? "{}")["identificacion"]?.Value<string>("ambiente"); }
        catch (Newtonsoft.Json.JsonException) { return null; }
    }

    public static string? Tipo(Factura factura) => factura.TipoDteCodigo ?? factura.TipoDTE switch
    {
        1 => "01", 2 or 3 => "03", 5 => "05", 6 => "06", 11 => "11", 14 => "14", 15 => "15", 18 => "18", _ => null
    };

    // Compras, donaciones y retornos no son ventas. Los pendientes no son ingreso fiscal aceptado.
    public static int SignoVenta(Factura factura) => !DteEstadoFiscal.EsAceptado(factura) ? 0 : Tipo(factura) switch
    {
        "01" or "03" or "06" or "11" => 1,
        "05" => -1,
        _ => 0
    };

    public static string UrlConsulta(Factura factura)
    {
        string? fecha = null;
        try { fecha = JObject.Parse(factura.JsonDte ?? "{}")["identificacion"]?.Value<string>("fecEmi"); }
        catch (Newtonsoft.Json.JsonException) { }
        return $"https://admin.factura.gob.sv/consultaPublica?ambiente={Ambiente(factura)}&codGen={Uri.EscapeDataString(factura.CodigoGeneracion)}&fechaEmi={Uri.EscapeDataString(fecha ?? factura.Fecha.ToString("yyyy-MM-dd"))}";
    }
}
