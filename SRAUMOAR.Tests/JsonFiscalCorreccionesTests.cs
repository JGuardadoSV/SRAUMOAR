using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;
using Xunit;

namespace SRAUMOAR.Tests;

public sealed class JsonFiscalCorreccionesTests
{
    [Theory]
    [InlineData("05", "00")]
    [InlineData("05", "01")]
    public void NotaParcialCumpleEsquemaYNoEscalaIvaDosVeces(string tipo, string ambiente)
    {
        var original = Original(ambiente);
        var factory = new DteJsonFactory();
        var json = factory.CrearNota(original.ToString(), tipo, $"DTE-{tipo}-M001P001-000000000000001",
            Guid.NewGuid().ToString(), 56.50m, "Ajuste parcial");
        var nota = JObject.Parse(json);
        Assert.Equal(6.5m, nota["resumen"]!.Value<decimal>("totalIva"));
        Assert.Equal(6.5m, nota["cuerpoDocumento"]![0]!.Value<decimal>("totalIva"));
        Assert.Equal(50m, nota["resumen"]!.Value<decimal>("totalGravada"));
        Assert.Null(((JObject)nota["emisor"]!).Property("tipoEstablecimiento"));
        Assert.Equal(ambiente, nota["identificacion"]!.Value<string>("ambiente"));
        factory.ValidarDocumento(json, ambiente, tipo);
    }

    [Fact]
    public void NotaHistoricaSinDistritoNoInventaCodigo()
    {
        var original = Original("00");
        ((JObject)original["receptor"]!["direccion"]!).Remove("distrito");
        var error = Assert.Throws<InvalidOperationException>(() => new DteJsonFactory().CrearNota(original.ToString(), "05",
            "DTE-05-M001P001-000000000000001", Guid.NewGuid().ToString(), 113m, "Ajuste"));
        Assert.Contains("distrito", error.Message);
    }

    [Fact]
    public void NotaParcialConRetencionConservaNetoYNoCuentaOtrosTributosComoIva()
    {
        var original = Original("00");
        original["resumen"]!["ivaRete1"] = 1m;
        original["resumen"]!["totalPagar"] = 112m;
        var json = new DteJsonFactory().CrearNota(original.ToString(), "05", "DTE-05-M001P001-000000000000001",
            Guid.NewGuid().ToString(), 56.5m, "Ajuste");
        var nota = JObject.Parse(json);
        Assert.Equal(56m, nota["resumen"]!.Value<decimal>("totalPagar"));
        Assert.Equal(0.5m, nota["resumen"]!.Value<decimal>("ivaRete"));
        Assert.Equal(0.5m, nota["cuerpoDocumento"]![0]!.Value<decimal>("ivaRete"));
    }

    [Fact]
    public void EsquemaDebitoCorruptoSeBloqueaSinInventarCamposFiscales()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new DteJsonFactory().CrearNota(Original("00").ToString(), "06",
            "DTE-06-M001P001-000000000000001", Guid.NewGuid().ToString(), 56.5m, "Ajuste"));
        Assert.Contains("nombre vacío", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidacionPermiteReceptorSinDocumentoNiContacto(bool receptorNulo)
    {
        var original = Original("01");
        original["identificacion"]!["tipoDte"] = "01";
        original["receptor"] = receptorNulo ? null : new JObject { ["nombre"] = "Consumidor final" };
        var json = new DteJsonFactory().CrearInvalidacion(original.ToString(), new string('S', 40), "Anulación de prueba",
            "Responsable", "06140101231010");
        var evento = JObject.Parse(json);
        Assert.Equal(JTokenType.Null, evento["documento"]!["numDocumento"]!.Type);
        Assert.Equal(JTokenType.Null, evento["documento"]!["correo"]!.Type);
        Assert.Equal("01", evento["identificacion"]!.Value<string>("ambiente"));
        DteSchemaValidator.Validar(json, "invalidacion", 3);
    }

    [Theory]
    [InlineData("Preparado", 0, false, "SELLO", true)]
    [InlineData("Preparado", 1, false, "SELLO", false)]
    [InlineData("Preparado", 0, false, null, false)]
    [InlineData("Aceptado", 1, false, "SELLO", true)]
    [InlineData("Aceptado", 1, true, "SELLO", false)]
    [InlineData("Incierto", 1, false, "SELLO", false)]
    public void CompatibilidadHistoricaRequiereSelloYNoReinterpretaIntentos(string estado, int intentos, bool anulado, string? sello, bool esperado)
        => Assert.Equal(esperado, DteEstadoFiscal.EsAceptado(new Factura
        { EstadoTransmision = estado, IntentosTransmision = intentos, Anulada = anulado, SelloRecepcion = sello }));

    [Fact]
    public void ExportacionNoSeConfundeConSujetoExcluido()
        => Assert.Equal("11", new DteJsonFactory().ObtenerTipoCanonico("11"));

    [Fact]
    public void IvaYMontosNoDependenDelSeparadorDecimalDelServidor()
    {
        var anterior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
            var nota = JObject.Parse(new DteJsonFactory().CrearNota(Original("00").ToString(), "05",
                "DTE-05-M001P001-000000000000001", Guid.NewGuid().ToString(), 56.5m, "Ajuste"));
            Assert.Equal(6.5m, nota["resumen"]!.Value<decimal>("totalIva"));
            Assert.Equal(56.5m, nota["resumen"]!.Value<decimal>("totalPagar"));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = anterior; }
    }

    private static JObject Original(string ambiente)
    {
        var direccion = new JObject { ["departamento"] = "06", ["municipio"] = "23", ["distrito"] = "01", ["complemento"] = "Dirección de prueba" };
        return new JObject
        {
            ["identificacion"] = new JObject { ["ambiente"] = ambiente, ["tipoDte"] = "03", ["codigoGeneracion"] = Guid.NewGuid().ToString().ToUpperInvariant(), ["fecEmi"] = "2026-08-26", ["numeroControl"] = "DTE-03-M001P001-000000000000001" },
            ["emisor"] = new JObject { ["nit"] = "06140101231010", ["nrc"] = "1234567", ["nombre"] = "Emisor de prueba", ["codActividad"] = "62010", ["descActividad"] = "Programación", ["nombreComercial"] = null, ["tipoEstablecimiento"] = "01", ["direccion"] = direccion.DeepClone(), ["telefono"] = "22222222", ["correo"] = "emisor@example.test", ["codEstableMH"] = "M001", ["codEstable"] = "M001", ["codPuntoVentaMH"] = "P001", ["codPuntoVenta"] = "P001" },
            ["receptor"] = new JObject { ["nit"] = "06140101231010", ["nrc"] = "1234567", ["nombre"] = "Receptor de prueba", ["codActividad"] = "62010", ["descActividad"] = "Programación", ["direccion"] = direccion.DeepClone(), ["telefono"] = null, ["correo"] = null },
            ["cuerpoDocumento"] = new JArray(new JObject { ["numItem"] = 1, ["tipoItem"] = 1, ["cantidad"] = 1m, ["uniMedida"] = 59, ["descripcion"] = "Artículo", ["precioUni"] = 100m, ["ventaGravada"] = 100m, ["tributos"] = new JArray("20") }),
            ["resumen"] = new JObject { ["totalGravada"] = 100m, ["subTotalVentas"] = 100m, ["montoTotalOperacion"] = 113m, ["totalPagar"] = 113m, ["tributos"] = new JArray(new JObject { ["codigo"] = "20", ["descripcion"] = "IVA", ["valor"] = 13m }) }
        };
    }
}
