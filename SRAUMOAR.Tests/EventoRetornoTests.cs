using Newtonsoft.Json.Linq;
using QuestPDF.Infrastructure;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;
using Xunit;

namespace SRAUMOAR.Tests;

public sealed class EventoRetornoTests
{
    [Fact]
    public void EretValidoEsReconocidoComoTipo18()
    {
        var factory = new DteJsonFactory();
        var json = CrearEret("00").ToString(Newtonsoft.Json.Formatting.None);

        factory.ValidarDocumento(json, "00", DteTipos.EventoRetorno);

        Assert.Equal(DteTipos.EventoRetorno, factory.ObtenerTipoCanonico("18"));
    }

    [Fact]
    public void EretNoPuedeTransmitirseConOtroAmbiente()
    {
        var factory = new DteJsonFactory();
        var json = CrearEret("00").ToString(Newtonsoft.Json.Formatting.None);

        Assert.Throws<InvalidOperationException>(() => factory.ValidarDocumento(json, "01", DteTipos.EventoRetorno));
    }

    [Fact]
    public void EretSinCampoFiscalRequeridoEsRechazado()
    {
        var factory = new DteJsonFactory();
        var documento = CrearEret("00");
        ((JObject)((JArray)documento["cuerpoDocumento"]!)[0]!).Remove("cantidad");

        Assert.Throws<InvalidOperationException>(() => factory.ValidarDocumento(
            documento.ToString(Newtonsoft.Json.Formatting.None), "00", DteTipos.EventoRetorno));
    }

    [Fact]
    public void EsquemaOficialRechazaPropiedadesAdicionales()
    {
        var factory = new DteJsonFactory();
        var documento = CrearEret("00");
        documento["campoNoOficial"] = true;

        Assert.Throws<InvalidOperationException>(() => factory.ValidarDocumento(
            documento.ToString(Newtonsoft.Json.Formatting.None), "00", DteTipos.EventoRetorno));
    }

    [Fact]
    public void EsquemaOficialRechazaPrecisionInvalida()
    {
        var factory = new DteJsonFactory();
        var documento = CrearEret("01");
        ((JObject)((JArray)documento["cuerpoDocumento"]!)[0]!)["cantidad"] = 1.123456789m;

        Assert.Throws<InvalidOperationException>(() => factory.ValidarDocumento(
            documento.ToString(Newtonsoft.Json.Formatting.None), "01", DteTipos.EventoRetorno));
    }

    [Fact]
    public void EsquemaOficialPermiteDocumentoNulo()
    {
        var factory = new DteJsonFactory();
        var documento = CrearEret("00");
        documento["documento"] = null;

        factory.ValidarDocumento(documento.ToString(Newtonsoft.Json.Formatting.None), "00", DteTipos.EventoRetorno);
    }

    [Fact]
    public void PdfEretIncluyeContenidoYSeGeneraSinServiciosExternos()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var servicio = new DtePdfService();

        var pdf = servicio.Generar("18", CrearEret("01").ToString(Newtonsoft.Json.Formatting.None), new string('S', 40));

        Assert.True(pdf.Length > 1000);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    private static JObject CrearEret(string ambiente) => new()
    {
        ["identificacion"] = new JObject { ["version"] = 1, ["ambiente"] = ambiente, ["tipoModelo"] = 1, ["tipoOperacion"] = 1,
            ["tipoEvento"] = "18", ["tipoContingencia"] = null, ["motivoContin"] = null, ["codigoGeneracion"] = Guid.NewGuid().ToString().ToUpperInvariant(),
            ["fecEmi"] = "2026-08-26", ["horEmi"] = "12:00:00", ["fusion"] = null, ["tipoMoneda"] = "USD" },
        ["documentoRelacionado"] = new JArray(new JObject { ["tipoDocumento"] = "01", ["codigoGeneracion"] = Guid.NewGuid().ToString().ToUpperInvariant(), ["fechaEmision"] = "2026-08-25" }),
        ["emisor"] = new JObject { ["nit"] = "06140101231010", ["nombre"] = "Emisor", ["codEstableMH"] = "M001", ["codEstable"] = "M001",
            ["codPuntoVentaMH"] = "P001", ["codPuntoVenta"] = "P001", ["recintoFiscal"] = null, ["tipoRegimen"] = null, ["regimen"] = null, ["tipoItemExpor"] = null },
        ["documento"] = new JObject { ["tipoDocumento"] = "13", ["numDocumento"] = "01234567-8", ["nombre"] = "Receptor",
            ["codPais"] = null, ["nombrePais"] = null, ["telefono"] = "22223333", ["correo"] = "receptor@example.com" },
        ["ventaTercero"] = null, ["compraTercero"] = null,
        ["cuerpoDocumento"] = new JArray(new JObject { ["numItem"] = 1, ["tipoItem"] = 1, ["codigoGeneracion"] = Guid.NewGuid().ToString().ToUpperInvariant(),
            ["cantidad"] = 1m, ["precioUni"] = 11.30m, ["descripcion"] = "Producto", ["codigo"] = "P1", ["uniMedida"] = 59,
            ["montoDescu"] = 0m, ["codTributo"] = null, ["ventaNoSuj"] = 0m, ["ventaExenta"] = 0m, ["ventaGravada"] = 11.30m,
            ["compra"] = 0m, ["tributos"] = null, ["psv"] = 0m, ["ivaItem"] = 1.30m, ["noGravado"] = 0m,
            ["seguro"] = 0m, ["flete"] = 0m, ["ivaRete"] = 0m, ["reteRenta"] = 0m }),
        ["resumen"] = new JObject { ["totalNoSuj"] = 0m, ["totalExenta"] = 0m, ["totalGravada"] = 11.30m, ["totalCompraExcluidos"] = 0m,
            ["subTotalVentas"] = 11.30m, ["tributos"] = null, ["totalSeguro"] = 0m, ["totalFlete"] = 0m, ["montoTotalOperacion"] = 11.30m,
            ["ivaRete"] = 0m, ["reteRenta"] = 0m, ["totalNoGravado"] = 0m, ["totalPagar"] = 11.30m,
            ["totalLetras"] = "ONCE 30/100 DÓLARES", ["totalNoOnerosas"] = 0m, ["totalIva"] = 1.30m, ["saldoFavor"] = 0m },
        ["apendice"] = null
    };
}
