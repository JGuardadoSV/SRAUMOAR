using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;
using Xunit;

namespace SRAUMOAR.Tests;

// Sin Contexto, EF, transporte, firma ni correo: solo JSON y catálogos en memoria.
public sealed class DocumentosActualizadosTests
{
    [Theory]
    [InlineData("01", 2)]
    [InlineData("03", 4)]
    [InlineData("14", 2)]
    [InlineData("15", 2)]
    public void BorradorActualizadoCumpleEsquemaCompleto(string tipo, int version)
    {
        var original = Borrador(tipo).ToString(Formatting.None);
        var factory = new DteJsonFactory();

        var json = DteDocumentoActualizador.ActualizarBorrador(original, "00", factory);
        var resultado = JObject.Parse(json);

        factory.ValidarDocumento(json, "00", tipo);
        Assert.Equal(version, resultado["identificacion"]!.Value<int>("version"));
        Assert.Equal(version, DteDocumentoActualizador.ObtenerVersion(tipo));
        Assert.NotNull(resultado["emisor"]);
        Assert.NotNull(resultado["receptor"]);
        Assert.Null(resultado["donatario"]);
        Assert.Null(resultado["donante"]);
        Assert.Null(resultado["sujetoExcluido"]);
        Assert.Null(resultado["emisor"]!["tipoEstablecimiento"]);
        Assert.Null(resultado["emisor"]!["codEstableMH"]);
        Assert.Null(resultado["emisor"]!["codPuntoVentaMH"]);
        Assert.Equal("M001", resultado["emisor"]!.Value<string>("codEstable"));
        Assert.Equal("33", resultado["receptor"]!["direccion"]!.Value<string>("distrito"));
        Assert.Equal(tipo == "03" ? 3 : 1, JObject.Parse(original)["identificacion"]!.Value<int>("version"));
        Assert.Equal(JObject.Parse(original)["identificacion"]!["codigoGeneracion"], resultado["identificacion"]!["codigoGeneracion"]);
        // Idempotencia: no vuelve a transformar campos ya adaptados.
        Assert.Equal(json, DteDocumentoActualizador.ActualizarBorrador(json, "00", factory));
    }

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    public void FacturasConservanRetencionYObservacionesSinCamposProhibidos(string tipo)
    {
        var documento = Borrador(tipo);
        documento["resumen"]!["ivaRete1"] = 1m;
        var resultado = Actualizar(documento);

        Assert.Null(resultado["extension"]);
        Assert.Null(resultado["resumen"]!["ivaRete1"]);
        Assert.Null(resultado["resumen"]!["reteRenta"]);
        Assert.Equal(1m, resultado["resumen"]!.Value<decimal>("ivaRete"));
        Assert.Equal("Observación de prueba", resultado["resumen"]!.Value<string>("observaciones"));
        if (tipo == "03")
        {
            Assert.Null(resultado["resumen"]!["ivaPerci1"]);
            Assert.Equal(0m, resultado["resumen"]!.Value<decimal>("ivaPerci"));
            Assert.Equal("06140101231010", resultado["receptor"]!.Value<string>("nit"));
            Assert.Null(resultado["receptor"]!["tipoDocumento"]);
        }
    }

    [Fact]
    public void SujetoExcluidoConservaRentaPeroNoIvaRetenidoLegacy()
    {
        var resultado = Actualizar(Borrador("14"));
        Assert.Null(resultado["resumen"]!["ivaRete1"]);
        Assert.Equal(10m, resultado["resumen"]!.Value<decimal>("reteRenta"));
        Assert.Equal(90m, resultado["resumen"]!.Value<decimal>("totalPagar"));
    }

    [Fact]
    public void DonacionRenombraDepreciacionYConservaDatosDeDonante()
    {
        var resultado = Actualizar(Borrador("15"));
        var item = resultado["cuerpoDocumento"]![0]!;
        Assert.Null(item["depreciacion"]);
        Assert.Equal(0m, item.Value<decimal>("tipoDepreciacion"));
        Assert.Equal("SV", resultado["receptor"]!.Value<string>("codPais"));
        Assert.Equal(1, resultado["receptor"]!.Value<int>("codDomiciliado"));
        Assert.Equal(100m, resultado["resumen"]!.Value<decimal>("valorTotal"));
    }

    [Theory]
    [InlineData("01", "reteRenta")]
    [InlineData("03", "reteRenta")]
    [InlineData("14", "ivaRete1")]
    public void NoDescartaImportesQueYaNoAdmiteElEsquema(string tipo, string campo)
    {
        var documento = Borrador(tipo);
        documento["resumen"]![campo] = 1m;
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));
    }

    [Theory]
    [InlineData("01")]
    [InlineData("03")]
    [InlineData("14")]
    [InlineData("15")]
    public void ValidaElEsquemaAntesDeDevolverUnJson(string tipo)
    {
        var documento = Borrador(tipo);
        documento["campoNoPermitido"] = true;
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));

        documento = Borrador(tipo);
        ((JObject)documento["cuerpoDocumento"]![0]!).Remove("cantidad");
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));

        documento = Borrador(tipo);
        documento["cuerpoDocumento"]![0]!["cantidad"] = 1.123456789m;
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));

        Assert.Throws<InvalidOperationException>(() => DteDocumentoActualizador.ActualizarBorrador(
            Borrador(tipo).ToString(), "01", new DteJsonFactory()));
    }

    [Theory]
    [InlineData("01", "receptor")]
    [InlineData("03", "receptor")]
    [InlineData("14", "sujetoExcluido")]
    [InlineData("15", "donante")]
    public void NoInventaDistritoCuandoFalta(string tipo, string receptor)
    {
        var documento = Borrador(tipo);
        ((JObject)documento[receptor]!["direccion"]!).Remove("distrito");
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));
    }

    [Fact]
    public void DocumentoYaFirmadoNoEsUnBorrador()
    {
        var documento = Borrador("01");
        documento["firmaElectronica"] = "firma-de-prueba";
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));
    }

    [Fact]
    public void NoSobrescribeCamposContradictorios()
    {
        var documento = Borrador("01");
        documento["resumen"]!["ivaRete"] = 3m;
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));
    }

    [Fact]
    public void NoActualizaNotasNiContingenciasNiVersionesDesconocidas()
    {
        Assert.Throws<InvalidOperationException>(() => DteDocumentoActualizador.ObtenerVersion("05"));
        Assert.Throws<InvalidOperationException>(() => DteDocumentoActualizador.ObtenerVersion("06"));
        var documento = Borrador("01");
        documento["identificacion"]!["tipoOperacion"] = 2;
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));
        documento = Borrador("01");
        documento["identificacion"]!["version"] = 99;
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));
    }

    [Fact]
    public void ElEmisorTambienDebeTenerDistritoExplicito()
    {
        var documento = Borrador("01");
        ((JObject)documento["emisor"]!["direccion"]!).Remove("distrito");
        Assert.Throws<InvalidOperationException>(() => Actualizar(documento));
    }

    [Fact]
    public void DistritoSeObtieneDeIdFiscalYNoDelIdLegacyONumeroFijo()
    {
        Assert.Equal("33", Resolver(700));
        Assert.Throws<InvalidOperationException>(() => Resolver(null));
        Assert.Throws<InvalidOperationException>(() => Resolver(1));
        Assert.Throws<InvalidOperationException>(() => Resolver(33));
        Assert.Throws<InvalidOperationException>(() => Resolver(700, activo: false));
        Assert.Throws<InvalidOperationException>(() => Resolver(700, departamento: "99"));
        Assert.Throws<InvalidOperationException>(() => Resolver(700, municipio: "99"));
        Assert.Throws<InvalidOperationException>(() => Resolver(700, version: "otra"));
    }

    [Fact]
    public void CatalogoAusenteODistritoAmbiguoBloqueanEmision()
    {
        var distrito = Item(700, "CAT-008", "33");
        Assert.Throws<InvalidOperationException>(() => DteDocumentoActualizador.ResolverDistritoReceptor(
            "04", "35", 700, [], [], [distrito]));
        Assert.Throws<InvalidOperationException>(() => DteDocumentoActualizador.ResolverDistritoReceptor(
            "04", "35", 700, [Item(1, "CAT-012", "04")], [Item(2, "CAT-013", "35")], [distrito, distrito]));
    }

    private static string Resolver(long? id, bool activo = true, string departamento = "04", string municipio = "35", string version = "1.1")
    {
        var distrito = Item(700, "CAT-008", "33");
        distrito.AmbitoDepartamento = "04";
        distrito.AmbitoMunicipio = "35";
        distrito.Activo = activo;
        distrito.Version = version;
        var municipioFiscal = Item(2, "CAT-013", "35");
        municipioFiscal.AmbitoDepartamento = "04";
        municipioFiscal.AmbitoMunicipio = "35";
        return DteDocumentoActualizador.ResolverDistritoReceptor(departamento, municipio, id,
            [Item(1, "CAT-012", "04")], [municipioFiscal], [distrito]);
    }

    private static CatalogoFeItem Item(long id, string catalogo, string codigo) => new()
    { Id = id, CodigoCatalogo = catalogo, Codigo = codigo, Descripcion = "Selección explícita", Version = "1.1", Activo = true };

    private static JObject Actualizar(JObject documento) => JObject.Parse(
        DteDocumentoActualizador.ActualizarBorrador(documento.ToString(Formatting.None), "00", new DteJsonFactory()));

    private static JObject Direccion() => new()
    { ["departamento"] = "04", ["municipio"] = "35", ["distrito"] = "33", ["complemento"] = "Dirección de prueba" };

    private static JObject Borrador(string tipo)
    {
        var identificacion = new JObject
        {
            ["version"] = tipo == "03" ? 3 : 1, ["ambiente"] = "00", ["tipoDte"] = tipo,
            ["numeroControl"] = $"DTE-{tipo}-M001P001-000000000000001",
            ["codigoGeneracion"] = "AAAAAAAA-BBBB-4CCC-8DDD-EEEEEEEEEEEE",
            ["tipoModelo"] = 1, ["tipoOperacion"] = 1,
            ["fecEmi"] = "2026-08-31", ["horEmi"] = "12:00:00", ["tipoMoneda"] = "USD"
        };
        if (tipo != "15") { identificacion["tipoContingencia"] = null; identificacion["motivoContin"] = null; }
        var emisor = new JObject
        {
            ["nit"] = "06140101231010", ["nrc"] = "1234567", ["nombre"] = "Emisor de prueba",
            ["codActividad"] = "85499", ["descActividad"] = "Enseñanza superior universitaria",
            ["direccion"] = Direccion(), ["telefono"] = "22223333", ["correo"] = "emisor@example.com",
            ["codEstable"] = "M001", ["codPuntoVenta"] = "P001",
            ["codEstableMH"] = "M001", ["codPuntoVentaMH"] = "P001"
        };
        if (tipo != "14") { emisor["nombreComercial"] = "Comercio"; emisor["tipoEstablecimiento"] = "02"; }
        if (tipo == "15") { emisor.Remove("nit"); emisor["tipoDocumento"] = "36"; emisor["numDocumento"] = "06140101231010"; }
        var receptor = new JObject
        {
            ["nombre"] = "Receptor de prueba", ["codActividad"] = "85499", ["descActividad"] = "Enseñanza superior universitaria",
            ["direccion"] = Direccion(), ["telefono"] = "22224444", ["correo"] = "receptor@example.com"
        };
        if (tipo != "14") receptor["nrc"] = null;
        if (tipo == "03") { receptor["nit"] = "06140101231010"; receptor["nombreComercial"] = ""; }
        else { receptor["tipoDocumento"] = "13"; receptor["numDocumento"] = "01234567-8"; }
        if (tipo == "15") { receptor["codPais"] = "SV"; receptor["codDomiciliado"] = 1; }

        var item = new JObject
        {
            ["numItem"] = 1, ["cantidad"] = 1, ["codigo"] = "PRUEBA", ["uniMedida"] = 59, ["descripcion"] = "Servicio de prueba"
        };
        var resumen = new JObject { ["totalLetras"] = "CIEN DÓLARES CON CERO CENTAVOS" };
        var pago = new JObject { ["codigo"] = "01", ["montoPago"] = tipo == "14" ? 90m : 100m, ["referencia"] = null };
        if (tipo != "15") { pago["plazo"] = null; pago["periodo"] = null; }
        resumen["pagos"] = new JArray(pago);
        if (tipo == "15")
        {
            item["tipoDonacion"] = 1; item["depreciacion"] = 0; item["valorUni"] = 100; item["valor"] = 100;
            resumen["valorTotal"] = 100;
        }
        else
        {
            item["tipoItem"] = 2; item["precioUni"] = 100; item["montoDescu"] = 0;
            resumen["totalDescu"] = 0; resumen["subTotal"] = 100; resumen["condicionOperacion"] = 1;
            resumen["ivaRete1"] = 0; resumen["reteRenta"] = tipo == "14" ? 10m : 0m;
            resumen["totalPagar"] = tipo == "14" ? 90m : 100m;
            if (tipo == "14")
            {
                item["compra"] = 100; resumen["totalCompra"] = 100; resumen["descu"] = 0; resumen["observaciones"] = null;
            }
            else
            {
                item["numeroDocumento"] = null; item["codTributo"] = null; item["ventaNoSuj"] = 0;
                item["ventaExenta"] = 100; item["ventaGravada"] = 0; item["tributos"] = null; item["psv"] = 100; item["noGravado"] = 0;
                resumen["totalNoSuj"] = 0; resumen["totalExenta"] = 100; resumen["totalGravada"] = 0;
                resumen["subTotalVentas"] = 100; resumen["descuNoSuj"] = 0; resumen["descuExenta"] = 0;
                resumen["descuGravada"] = 0; resumen["porcentajeDescuento"] = 0; resumen["tributos"] = null;
                resumen["montoTotalOperacion"] = 100; resumen["totalNoGravado"] = 0; resumen["saldoFavor"] = 0; resumen["numPagoElectronico"] = null;
                if (tipo == "01") { item["ivaItem"] = 0; resumen["totalIva"] = 0; }
                else resumen["ivaPerci1"] = 0;
            }
        }
        var documento = new JObject
        {
            ["identificacion"] = identificacion, [tipo == "15" ? "donatario" : "emisor"] = emisor,
            [tipo == "15" ? "donante" : tipo == "14" ? "sujetoExcluido" : "receptor"] = receptor,
            ["cuerpoDocumento"] = new JArray(item), ["resumen"] = resumen, ["apendice"] = null
        };
        if (tipo is "01" or "03")
        {
            documento["documentoRelacionado"] = null; documento["ventaTercero"] = null; documento["otrosDocumentos"] = null;
            documento["extension"] = new JObject { ["observaciones"] = "Observación de prueba" };
        }
        if (tipo == "15") documento["otrosDocumentos"] = new JArray(new JObject
        { ["codDocAsociado"] = null, ["descDocumento"] = null, ["detalleDocumento"] = null });
        return documento;
    }
}
