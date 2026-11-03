using System.Net;
using System.Reflection;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Newtonsoft.Json.Linq;
using QuestPDF.Infrastructure;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;
using Xunit;

namespace SRAUMOAR.Tests;

public sealed class AuditoriaSeguimientoTests
{
    private const string Codigo = "12345678-1234-4234-8234-123456789ABC";

    [Fact]
    public void VentaNetaRestaCreditoYExcluyePendientesComprasDonacionesYRetornos()
    {
        var facturas = new[] { Factura("01", 100), Factura("05", 20), Factura("06", 5),
            Factura("01", 500, DteEstados.Rechazado), Factura("01", 300, DteEstados.Incierto),
            Factura("14", 100), Factura("15", 100), Factura("18", 100), Factura("01", 200, anulada: true) };
        Assert.Equal(85m, facturas.Sum(f => DteReporteReglas.SignoVenta(f) * f.TotalPagar));
        Assert.Equal(0, DteReporteReglas.SignoVenta(Factura("01", 10, DteEstados.Preparado)));
        var historica = Factura("03", 10, DteEstados.Preparado);
        historica.IntentosTransmision = 0;
        Assert.Equal(1, DteReporteReglas.SignoVenta(historica));
    }

    [Theory]
    [InlineData("00")]
    [InlineData("01")]
    public void AmbienteHistoricoYConsultaSeObtienenDelDocumento(string ambiente)
    {
        var factura = Factura("01", 10);
        factura.AmbienteFiscal = null;
        factura.Fecha = new DateTime(2026, 11, 2);
        factura.JsonDte = Documento(ambiente).ToString();
        Assert.Equal(ambiente, DteReporteReglas.Ambiente(factura));
        Assert.Contains($"ambiente={ambiente}", DteReporteReglas.UrlConsulta(factura));
        Assert.Contains("fechaEmi=2026-08-07", DteReporteReglas.UrlConsulta(factura));
    }

    [Fact]
    public void HistoricoSinAmbienteNoSeAtribuyeAProduccion()
    {
        var factura = Factura("01", 10);
        factura.AmbienteFiscal = null;
        factura.JsonDte = "json dañado";
        Assert.Null(DteReporteReglas.Ambiente(factura));
    }

    [Theory]
    [InlineData("totalPagar")]
    [InlineData("totalGravada")]
    [InlineData("totalIva")]
    public void EdicionDeImportesSeRechazaAunqueElJsonSeaValido(string campo)
    {
        var original = Documento();
        var editado = (JObject)original.DeepClone();
        editado["resumen"]![campo] = 25m;
        Assert.Throws<InvalidOperationException>(() => DteEdicionReglas.ValidarSinCambiosFinancieros(original, editado));
    }

    [Fact]
    public void NoPuedeAlterarseUnaLineaSinCambiarResumenPeroSiElContacto()
    {
        var original = Documento();
        var editado = (JObject)original.DeepClone();
        editado["cuerpoDocumento"]![0]!["precioUni"] = 42m;
        Assert.Throws<InvalidOperationException>(() => DteEdicionReglas.ValidarSinCambiosFinancieros(original, editado));
        editado = (JObject)original.DeepClone();
        editado["receptor"]!["correo"] = "corregido@example.invalid";
        DteEdicionReglas.ValidarSinCambiosFinancieros(original, editado);
    }

    [Fact]
    public void DosNotasIgualesSonDistintasOperacionesPeroUnRetryConservaIdentidad()
    {
        var request = new DteNotaRequest(23, "05", 10m, "Devolución", OperacionId: Guid.NewGuid());
        var clave = ClaveNota(request);
        Assert.Equal(clave, ClaveNota(request with { }));
        Assert.NotEqual(clave, ClaveNota(request with { OperacionId = Guid.NewGuid() }));
        var alterada = ClaveNota(request with { Monto = 11m });
        Assert.NotEqual(clave, alterada);
        Assert.Equal(clave[..35], alterada[..35]);
        Assert.True(clave.Length <= 100);
        Assert.Throws<TargetInvocationException>(() => ClaveNota(request with { OperacionId = Guid.Empty }));
    }

    [Theory]
    [InlineData("aceptada", true)]
    [InlineData("otra-factura", false)]
    [InlineData("otro-codigo", false)]
    [InlineData("otro-ambiente", false)]
    [InlineData("otro-json", false)]
    [InlineData("sin-sello", false)]
    [InlineData("incierta", false)]
    [InlineData("emision", false)]
    [InlineData("factura-aceptada", false)]
    public void InvalidacionSoloSeRecuperaConEvidenciaExacta(string caso, bool esperado)
    {
        var json = new JObject { ["identificacion"] = new JObject { ["codigoGeneracion"] = Codigo, ["ambiente"] = "00" },
            ["documento"] = new JObject { ["codigoGeneracion"] = "DOCUMENTO-ORIGINAL" } }.ToString();
        var factura = new Factura { FacturaId = 23, EstadoTransmision = DteEstados.Anulando, JsonAnulacion = json };
        var evento = new DteEvento { FacturaId = 23, TipoEvento = "INVALIDACION", CodigoEvento = Codigo,
            AmbienteFiscal = "00", Estado = DteEstados.Aceptado, SelloRecepcion = "SELLO-SINTETICO", JsonEvento = json };
        switch (caso)
        {
            case "otra-factura": evento.FacturaId++; break;
            case "otro-codigo": evento.CodigoEvento = Guid.NewGuid().ToString(); break;
            case "otro-ambiente": evento.AmbienteFiscal = "01"; break;
            case "otro-json": evento.JsonEvento = "{}"; break;
            case "sin-sello": evento.SelloRecepcion = null; break;
            case "incierta": evento.Estado = DteEstados.Incierto; break;
            case "emision": evento.TipoEvento = "EMISION"; break;
            case "factura-aceptada": factura.EstadoTransmision = DteEstados.Aceptado; break;
        }
        var recuperar = (bool)typeof(DteConciliacionService).GetMethod("PuedeRecuperarInvalidacion", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { factura, evento })!;
        Assert.Equal(esperado, recuperar);
    }

    [Theory]
    [InlineData("PROCESADO", Codigo, "00", "SELLO-SINTETICO", true)]
    [InlineData("RECHAZADO", Codigo, "00", "SELLO-SINTETICO", false)]
    [InlineData("PROCESADO", "OTRO", "00", "SELLO-SINTETICO", false)]
    [InlineData("PROCESADO", Codigo, "01", "SELLO-SINTETICO", false)]
    [InlineData("PROCESADO", Codigo, "00", "null", false)]
    [InlineData("PROCESADO", Codigo, "00", "EVENTO_EXISTENTE", false)]
    [InlineData("PROCESADO", Codigo, "00", "", false)]
    public void ConsultaExigeEvidenciaPositivaDeLaMismaIdentidad(string estado, string codigo, string ambiente, string sello, bool esperado)
    {
        var body = new JObject { ["estado"] = estado, ["codigoGeneracion"] = codigo, ["ambiente"] = ambiente, ["selloRecibido"] = sello }.ToString();
        Assert.Equal(esperado, DteConsultaClient.Interpretar(body, Codigo, "00").Aceptado);
        Assert.Equal(esperado, DteConsultaClient.Interpretar(new JObject { ["resultado"] = JObject.Parse(body) }.ToString(), Codigo, "00").Aceptado);
    }

    [Theory]
    [InlineData("{\"estado\":{}} ")]
    [InlineData("{\"estado\":\"PROCESADO\",\"selloRecibido\":{}} ")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("<html>Error</html>")]
    [InlineData("{\"estado\":\"RECHAZADO\",\"descripcionMsg\":\"Documento no encontrado\"}")]
    public void UnaConsultaAmbiguaNoLiberaReservas(string body)
        => Assert.False(DteConsultaClient.Interpretar(body, Codigo, "00").Aceptado);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClienteSoloConsultaSinFirmarNiEmitir(bool externa)
    {
        using var http = new HttpDoble(async (request, _) =>
        {
            Assert.EndsWith(externa ? "/api/consulta-dte" : "/fesv/recepcion/consultadte/", request.RequestUri!.AbsolutePath);
            var body = JObject.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(Codigo, body.Value<string>("codigoGeneracion"));
            Assert.Equal("03", body.Value<string>("tdte"));
            Assert.Null(body["documento"]);
            Assert.Null(body["dteJson"]);
            Assert.Null(body["passwordPrivado"]);
            if (externa) Assert.Equal("00", body.Value<string>("ambiente"));
            else Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Respuesta(200, Evidencia());
        });
        var token = new TokenDoble();
        var config = Configuracion(externa);
        var resultado = await new DteConsultaClient(http, token).ConsultarAsync(config, "06140101231010", "03", Codigo);
        Assert.True(resultado.Aceptado);
        Assert.Equal(1, http.Llamadas);
        Assert.Equal(externa ? 0 : 1, token.Obtenciones);
    }

    [Fact]
    public async Task ConsultaHttpFallidaNoAcreditaAceptacionAunqueIncluyaSello()
    {
        using var http = new HttpDoble((_, _) => Task.FromResult(Respuesta(500, Evidencia())));
        var resultado = await new DteConsultaClient(http, new TokenDoble()).ConsultarAsync(Configuracion(), "06140101231010", "01", Codigo);
        Assert.False(resultado.Aceptado);
        Assert.Equal(1, http.Llamadas);
    }

    [Fact]
    public async Task ConsultaRenuevaTokenSoloTras401()
    {
        var llamada = 0;
        using var http = new HttpDoble((_, _) => Task.FromResult(++llamada == 1 ? Respuesta(401, "{}") : Respuesta(200, Evidencia())));
        var tokens = new TokenDoble();
        var resultado = await new DteConsultaClient(http, tokens).ConsultarAsync(Configuracion(), "06140101231010", "01", Codigo);
        Assert.True(resultado.Aceptado);
        Assert.Equal(2, http.Llamadas);
        Assert.Equal(1, tokens.Invalidaciones);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(150)]
    public void PdfIncluyeDatosFiscalesDesgloseYConsultaConImagenQr(int lineas)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var documento = Documento();
        var cuerpo = (JArray)documento["cuerpoDocumento"]!;
        var linea = (JObject)cuerpo[0]!;
        for (var i = 1; i < lineas; i++)
        {
            var nueva = (JObject)linea.DeepClone();
            nueva["numItem"] = i + 1;
            cuerpo.Add(nueva);
        }
        var pdf = new DtePdfService().Generar("01", documento.ToString(), "SELLO-SINTETICO");
        using var reader = new PdfReader(new MemoryStream(pdf));
        using var archivo = new PdfDocument(reader);
        var texto = string.Join("\n", Enumerable.Range(1, archivo.GetNumberOfPages()).Select(i => PdfTextExtractor.GetTextFromPage(archivo.GetPage(i))));
        Assert.Contains("NRC-PRUEBA", texto);
        Assert.Contains("DIRECCION-PRUEBA", texto);
        Assert.Contains("Retención", texto);
        Assert.Contains("SELLO-SINTETICO", texto);
        Assert.Contains("ambiente=00", texto);
        Assert.Contains("fechaEmi=2026-08-07", texto);
        var imagenes = Enumerable.Range(1, archivo.GetNumberOfPages()).SelectMany(i =>
        {
            var recursos = archivo.GetPage(i).GetResources().GetResource(PdfName.XObject);
            return recursos is null ? Enumerable.Empty<PdfStream>() : recursos.KeySet().Select(k => recursos.GetAsStream(k)).Where(x => x is not null);
        });
        Assert.Contains(imagenes, x => PdfName.Image.Equals(x.GetAsName(PdfName.Subtype)));
        if (lineas > 1) Assert.True(archivo.GetNumberOfPages() > 1);
    }

    private static string ClaveNota(DteNotaRequest request) => (string)typeof(DteNotaService)
        .GetMethod("ClaveOperacion", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { request })!;

    private static Factura Factura(string tipo, decimal total, string estado = DteEstados.Aceptado, bool anulada = false)
        => new() { TipoDteCodigo = tipo, TotalPagar = total, EstadoTransmision = estado, Anulada = anulada,
            SelloRecepcion = "SELLO-SINTETICO", IntentosTransmision = 1, CodigoGeneracion = Codigo, AmbienteFiscal = "00" };

    private static JObject Documento(string ambiente = "00") => new()
    {
        ["identificacion"] = new JObject { ["ambiente"] = ambiente, ["tipoDte"] = "01", ["codigoGeneracion"] = Codigo,
            ["fecEmi"] = "2026-08-07", ["horEmi"] = "12:30:00", ["numeroControl"] = "DTE-01-M001P001-000000000000001" },
        ["emisor"] = new JObject { ["nit"] = "06140101231010", ["nrc"] = "NRC-PRUEBA", ["nombre"] = "Emisor sintético",
            ["direccion"] = new JObject { ["complemento"] = "DIRECCION-PRUEBA", ["departamento"] = "05", ["municipio"] = "01", ["distrito"] = "01" } },
        ["receptor"] = new JObject { ["nombre"] = "Receptor sintético", ["correo"] = "prueba@example.invalid" },
        ["cuerpoDocumento"] = new JArray(new JObject { ["numItem"] = 1, ["descripcion"] = "Servicio de prueba", ["codigo"] = "P1", ["cantidad"] = 1,
            ["precioUni"] = 11.30m, ["montoDescu"] = 0, ["ventaGravada"] = 11.30m, ["ventaExenta"] = 0, ["ventaNoSuj"] = 0, ["ivaItem"] = 1.30m }),
        ["resumen"] = new JObject { ["totalPagar"] = 11.30m, ["totalGravada"] = 11.30m, ["totalIva"] = 1.30m, ["ivaRete"] = 0, ["reteRenta"] = 0 }
    };

    private static ConfiguracionFeActiva Configuracion(bool externa = false) => new(new ConfiguracionFacturacionElectronica
    {
        AmbienteFiscal = "00", Habilitado = true, UsuarioHacienda = "sintetico", UsarApiExterna = externa,
        UrlConsulta = "https://transporte.invalid/fesv/recepcion/consultadte/", UrlApiExterna = "https://transporte.invalid"
    }, "credencial-sintetica", "password-sintetico");

    private static string Evidencia() => new JObject { ["estado"] = "PROCESADO", ["codigoGeneracion"] = Codigo, ["ambiente"] = "00", ["selloRecibido"] = "SELLO-SINTETICO" }.ToString();
    private static HttpResponseMessage Respuesta(int codigo, string body) => new((HttpStatusCode)codigo) { Content = new StringContent(body) };
    private sealed class TokenDoble : ITokenHaciendaService
    {
        public int Obtenciones;
        public int Invalidaciones;
        public Task<string> ObtenerAsync(ConfiguracionFeActiva config, CancellationToken ct = default) { Obtenciones++; return Task.FromResult("token-sintetico"); }
        public void Invalidar(string ambiente, string? usuario = null) => Invalidaciones++;
    }
    private sealed class HttpDoble(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler, IHttpClientFactory
    {
        public int Llamadas;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Llamadas++;
            Assert.Equal("transporte.invalid", request.RequestUri!.Host);
            return responder(request, ct);
        }
    }
}
