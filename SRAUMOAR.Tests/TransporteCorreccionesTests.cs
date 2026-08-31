using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;
using Xunit;

namespace SRAUMOAR.Tests;

// Sin Contexto, proveedores EF, app ni red. HTTP siempre interceptado; archivos/llaves sólo sintéticos.
// Reflection permite probar los límites de transporte sin ampliar interfaces de producción.
public sealed class TransporteCorreccionesTests
{
    private const string Nit = "06140101231010";
    private const string Codigo = "12345678-1234-4234-8234-123456789ABC";
    private const string Jws = "e30.e30.c2ln";

    [Fact]
    public async Task FirmadorExternoEnviaPasswordPriYObjetoYLeeBodyString()
    {
        string? enviado = null;
        using var http = new HttpSimulado(async (request, ct) =>
        {
            Assert.Equal("/firmardocumento/", request.RequestUri!.AbsolutePath);
            enviado = await request.Content!.ReadAsStringAsync(ct);
            return Respuesta(200, JsonSerializer.Serialize(new { status = "OK", body = Jws }));
        });
        var config = Configuracion();
        config.Entidad.UsarFirmadorExterno = true;
        var servicio = Servicio(http);

        var firmado = await InvocarAsync<string>(servicio, "FirmarAsync", config, Documento(), CancellationToken.None);

        Assert.Equal(Jws, firmado);
        using var body = JsonDocument.Parse(enviado!);
        Assert.Equal("password-sintetico", body.RootElement.GetProperty("passwordPri").GetString());
        Assert.Equal(Nit, body.RootElement.GetProperty("nit").GetString());
        Assert.True(body.RootElement.GetProperty("activo").GetBoolean());
        Assert.Equal(JsonValueKind.Object, body.RootElement.GetProperty("dteJson").ValueKind);
        Assert.False(body.RootElement.TryGetProperty("documento", out _));
        Assert.Equal(1, http.Llamadas);
    }

    [Theory]
    [InlineData("{\"body\":{\"documento\":\"e30.e30.c2ln\"}}")]
    [InlineData("{\"body\":null}")]
    [InlineData("{\"body\":\"\"}")]
    [InlineData("{\"body\":\"no-es-jws\"}")]
    [InlineData("{\"status\":\"ERROR\",\"body\":\"e30.e30.c2ln\"}")]
    [InlineData("[]")]
    public async Task RespuestaFirmadorInvalidaEsPreparadoSinRecepcion(string body)
    {
        using var http = new HttpSimulado((_, _) => Task.FromResult(Respuesta(200, body)));
        var config = Configuracion();
        config.Entidad.UsarFirmadorExterno = true;
        var token = new TokenSimulado();
        var servicio = Servicio(http, token: token);

        var resultado = await EvaluarEmision(servicio, config);

        Assert.Equal(DteEstados.Preparado, resultado.Estado);
        Assert.Equal(1, http.Llamadas);
        Assert.Equal(0, token.Obtenciones);
    }

    [Theory]
    [InlineData("firma")]
    [InlineData("token")]
    [InlineData("url")]
    [InlineData("cancelacion")]
    public async Task FalloAntesDelEnvioEsPreparado(string fallo)
    {
        using var http = SinHttp();
        var config = Configuracion();
        var firmador = new FirmadorSimulado { Error = fallo == "firma" ? new InvalidOperationException("Firma fallida") : null };
        var token = new TokenSimulado { FallarEn = fallo == "token" ? 1 : 0 };
        if (fallo == "url") config.Entidad.UrlRecepcion = "ruta-relativa";
        using var cancelacion = new CancellationTokenSource();
        if (fallo == "cancelacion") cancelacion.Cancel();
        var servicio = Servicio(http, firmador, token);

        var resultado = await EvaluarEmision(servicio, config, cancelacion.Token);

        Assert.Equal(DteEstados.Preparado, resultado.Estado);
        Assert.Equal(0, http.Llamadas);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("red")]
    [InlineData("cancelacion")]
    public async Task FalloDuranteEnvioEsInciertoSinReintento(string fallo)
    {
        using var http = new HttpSimulado((_, _) => Task.FromException<HttpResponseMessage>(fallo switch
        {
            "red" => new HttpRequestException("Conexión perdida"),
            "timeout" => new TaskCanceledException("Timeout"),
            _ => new OperationCanceledException()
        }));
        var token = new TokenSimulado();
        var resultado = await EvaluarEmision(Servicio(http, token: token), Configuracion());

        Assert.Equal(DteEstados.Incierto, resultado.Estado);
        Assert.Equal(1, http.Llamadas);
        Assert.Equal(0, token.Invalidaciones);
    }

    [Theory]
    [InlineData(503, "{}", "Incierto")]
    [InlineData(400, "{\"estado\":\"RECHAZADO\"}", "Rechazado")]
    [InlineData(200, "{\"selloRecibido\":\"sello-sintetico\"}", "Aceptado")]
    [InlineData(200, "{\"selloRecibido\":\" \"}", "Incierto")]
    [InlineData(200, "{\"estado\":42}", "Incierto")]
    [InlineData(200, "[]", "Incierto")]
    [InlineData(200, "no-json", "Incierto")]
    public async Task RespuestasDistinguenRechazoFiscalDeIncertidumbre(int status, string body, string estado)
    {
        using var http = new HttpSimulado((_, _) => Task.FromResult(Respuesta(status, body)));
        var token = new TokenSimulado();
        var resultado = await EvaluarEmision(Servicio(http, token: token), Configuracion());
        Assert.Equal(estado, resultado.Estado);
        Assert.Equal(1, http.Llamadas);
        Assert.Equal(0, token.Invalidaciones);
    }

    [Theory]
    [InlineData(200, "Aceptado")]
    [InlineData(401, "Preparado")]
    [InlineData(503, "Incierto")]
    public async Task Un401RenuevaUnaSolaVezSinVolverAFirmar(int segundoStatus, string esperado)
    {
        var documentos = new List<string>();
        var autorizaciones = new List<string>();
        using var http = new HttpSimulado(async (request, ct) =>
        {
            documentos.Add(await request.Content!.ReadAsStringAsync(ct));
            autorizaciones.Add(request.Headers.Authorization!.Parameter!);
            return Respuesta(documentos.Count == 1 ? 401 : segundoStatus,
                documentos.Count == 1 ? "" : "{\"selloRecibido\":\"sello\"}");
        });
        var token = new TokenSimulado();
        var firmador = new FirmadorSimulado();
        var resultado = await EvaluarEmision(Servicio(http, firmador, token), Configuracion());

        Assert.Equal(esperado, resultado.Estado);
        Assert.Equal(2, http.Llamadas);
        Assert.Equal(1, firmador.Llamadas);
        Assert.Equal(1, token.Invalidaciones);
        Assert.Equal(("00", "usuario-sintetico"), token.UltimaInvalidacion);
        Assert.Equal(new[] { "token-1", "token-2" }, autorizaciones);
        Assert.Equal(documentos[0], documentos[1]);
    }

    [Fact]
    public async Task FalloAlRenovarTras401EsPreparadoYNoEnviaOtraVez()
    {
        using var http = new HttpSimulado((_, _) => Task.FromResult(Respuesta(401, "")));
        var token = new TokenSimulado { FallarEn = 2 };
        var resultado = await EvaluarEmision(Servicio(http, token: token), Configuracion());
        Assert.Equal(DteEstados.Preparado, resultado.Estado);
        Assert.Equal(1, http.Llamadas);
        Assert.Equal(2, token.Obtenciones);
    }

    [Theory]
    [InlineData("Preparado", true)]
    [InlineData("Incierto", false)]
    [InlineData("Enviado", false)]
    [InlineData("Aceptado", false)]
    [InlineData("Rechazado", false)]
    public void SoloPreparadoPuedeReclamarTransmisionOEvento(string estado, bool esperado)
        => Assert.Equal(esperado, Invocar<bool>(null, typeof(DteTransmisionService), "PuedeReintentar", estado));

    [Theory]
    [InlineData("01", "00")]
    [InlineData("03", "01")]
    [InlineData("05", "00")]
    [InlineData("06", "01")]
    [InlineData("11", "00")]
    [InlineData("14", "01")]
    [InlineData("15", "00")]
    [InlineData("18", "01")]
    public async Task ReprocesoSeleccionaAmbienteDelJsonParaTodosLosTipos(string tipo, string ambiente)
    {
        using var http = SinHttp();
        var config = new ConfiguracionSimulada();
        var servicio = Servicio(http, configuracion: config);
        var request = new DteTransmisionRequest(Documento(ambiente, tipo), tipo, Codigo, "control", 1);

        var resultado = await InvocarAsync<ConfiguracionFeActiva>(servicio, "ObtenerConfiguracionDocumentoAsync", request, CancellationToken.None);

        Assert.Equal(ambiente, config.AmbienteSolicitado);
        Assert.Equal(ambiente, resultado.Entidad.AmbienteFiscal);
        Assert.Equal(0, config.ConsultasActiva);
        Assert.Equal(0, http.Llamadas);
    }

    [Fact]
    public async Task VersionDiscrepanteSeDetieneAntesDeFirmaYToken()
    {
        using var http = SinHttp();
        var servicio = Servicio(http);
        var request = new DteTransmisionRequest(Documento(), "01", Codigo, "control", 2);
        var registro = new DteTransmision { JsonEnviado = request.Json };
        var resultado = await Evaluar(servicio, () => InvocarAsync<HaciendaRespuesta>(servicio,
            "PrepararYEnviarDocumentoAsync", request, registro, CancellationToken.None));
        Assert.Equal(DteEstados.Preparado, resultado.Estado);
        Assert.Contains("identificacion.version", resultado.Error);
        Assert.Equal(0, http.Llamadas);
    }

    [Fact]
    public async Task SobreHaciendaConservaVersionAmbienteYJws()
    {
        using var http = new HttpSimulado(async (request, ct) =>
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(4, json.RootElement.GetProperty("version").GetInt32());
            Assert.Equal("01", json.RootElement.GetProperty("ambiente").GetString());
            Assert.Equal(Jws, json.RootElement.GetProperty("documento").GetString());
            Assert.Equal("03", json.RootElement.GetProperty("tipoDte").GetString());
            return Respuesta(200, "{\"selloRecibido\":\"sello\"}");
        });
        var respuesta = await new HaciendaDteClient(http).EnviarDteAsync(Configuracion("01"), "Bearer token", Jws, "03", 4, Codigo);
        Assert.True(respuesta.Exito);
    }

    [Theory]
    [InlineData("INVALIDACION", 3, false)]
    [InlineData("CONTINGENCIA", 4, false)]
    [InlineData("INVALIDACION", 3, true)]
    [InlineData("CONTINGENCIA", 4, true)]
    public async Task EventoInvalidoValidaEsquemaAntesDeFirmarOUsarApi(string tipo, int version, bool api)
    {
        using var http = SinHttp();
        var firmador = new FirmadorSimulado();
        var config = Configuracion();
        config.Entidad.UsarApiExterna = api;
        var servicio = Servicio(http, firmador);
        var resultado = await Evaluar(servicio, () => InvocarAsync<HaciendaRespuesta>(servicio,
            "EjecutarEventoAsync", config, Documento(version: version), tipo, CancellationToken.None));
        Assert.Equal(DteEstados.Preparado, resultado.Estado);
        Assert.Contains("no cumple", resultado.Error);
        Assert.Equal(0, firmador.Llamadas);
        Assert.Equal(0, http.Llamadas);
    }

    [Fact]
    public async Task ApiExternaSinUrlEsPreparado()
    {
        using var http = SinHttp();
        var config = Configuracion();
        config.Entidad.UsarApiExterna = true;
        config.Entidad.UrlApiExterna = null;
        var resultado = await EvaluarEmision(Servicio(http), config);
        Assert.Equal(DteEstados.Preparado, resultado.Estado);
        Assert.Equal(0, http.Llamadas);
    }

    [Fact]
    public async Task ApiExternaConCorteTrasEnviarEsIncierto()
    {
        using var http = new HttpSimulado((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Corte")));
        var config = Configuracion();
        config.Entidad.UsarApiExterna = true;
        var resultado = await EvaluarEmision(Servicio(http), config);
        Assert.Equal(DteEstados.Incierto, resultado.Estado);
        Assert.Equal(1, http.Llamadas);
    }

    [Fact]
    public async Task TokenSeComparteEntreScopesY401TardioNoInvalidaTokenNuevo()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var numero = 0;
        var inicio = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var continuar = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpSimulado(async (_, ct) =>
        {
            var n = Interlocked.Increment(ref numero);
            if (n == 1)
            {
                inicio.TrySetResult(true);
                await continuar.Task.WaitAsync(ct);
            }
            return Respuesta(200, JsonSerializer.Serialize(new { body = new { token = "Bearer " + TokenJwt(n) } }));
        });
        var a = new TokenHaciendaService(cache, http);
        var b = new TokenHaciendaService(cache, http);
        var config = Configuracion();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var primera = a.ObtenerAsync(config, limite.Token);
        await inicio.Task.WaitAsync(limite.Token);
        var segunda = b.ObtenerAsync(config, limite.Token);
        continuar.TrySetResult(true);
        var originales = await Task.WhenAll(primera, segunda);
        Assert.Equal(originales[0], originales[1]);
        Assert.Equal(1, http.Llamadas);
        Assert.DoesNotContain("Bearer", originales[0]);

        var renovados = await Task.WhenAll(
            InvocarAsync<string>(a, "RenovarTrasRechazoAsync", config, originales[0], limite.Token),
            InvocarAsync<string>(b, "RenovarTrasRechazoAsync", config, originales[0], limite.Token));
        Assert.Equal(renovados[0], renovados[1]);
        Assert.NotEqual(originales[0], renovados[0]);
        Assert.Equal(2, http.Llamadas);
        Assert.Equal(renovados[0], await b.ObtenerAsync(config, limite.Token));
    }

    [Fact]
    public async Task CacheTokenAislaAmbienteYUsuario()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var numero = 0;
        using var http = new HttpSimulado((_, _) => Task.FromResult(Respuesta(200,
            JsonSerializer.Serialize(new { token = TokenJwt(++numero) }))));
        var servicio = new TokenHaciendaService(cache, http);
        var cero = Configuracion();
        var uno = Configuracion("01");
        var otroUsuario = Configuracion();
        otroUsuario.Entidad.UsuarioHacienda = "otro-usuario";
        var tokenCero = await servicio.ObtenerAsync(cero);
        var tokenUno = await servicio.ObtenerAsync(uno);
        var tokenOtro = await servicio.ObtenerAsync(otroUsuario);
        servicio.Invalidar("00", "usuario-sintetico");
        Assert.Equal(tokenUno, await servicio.ObtenerAsync(uno));
        Assert.Equal(tokenOtro, await servicio.ObtenerAsync(otroUsuario));
        Assert.NotEqual(tokenCero, await servicio.ObtenerAsync(cero));
        Assert.Equal(4, http.Llamadas);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("nit")]
    [InlineData("privada")]
    [InlineData("base64")]
    [InlineData("sin-privada")]
    [InlineData("hash")]
    [InlineData("duplicado")]
    [InlineData("dtd")]
    public async Task CertificadoInvalidoNoReemplazaAnteriorNiDejaStaging(string caso)
    {
        using var carpeta = new CarpetaSintetica();
        using var rsa = RSA.Create(2048);
        var valido = Certificado(rsa);
        var ruta = await Guardar(carpeta.Storage, valido);
        var anterior = await File.ReadAllBytesAsync(ruta);
        var xml = XDocument.Parse(valido);
        switch (caso)
        {
            case "nit": xml.Root!.Element("nit")!.Value = "06140101239999"; break;
            case "privada": xml.Root!.Element("privateKey")!.Element("encodied")!.Value = "AQID"; break;
            case "base64": xml.Root!.Element("privateKey")!.Element("encodied")!.Value = "!!!"; break;
            case "sin-privada": xml.Root!.Element("privateKey")!.Remove(); break;
            case "hash": xml.Root!.Element("privateKey")!.Element("clave")!.Value = "no-hash"; break;
            case "duplicado": xml.Root!.Add(new XElement("nit", Nit)); break;
        }
        var invalido = caso == "xml" ? "<CertificadoMH>" : caso == "dtd"
            ? "<!DOCTYPE CertificadoMH [<!ENTITY prueba 'sintetica'>]>" + xml : xml.ToString();

        await Assert.ThrowsAnyAsync<Exception>(() => Guardar(carpeta.Storage, invalido));

        Assert.Equal(anterior, await File.ReadAllBytesAsync(ruta));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(ruta)!));
    }

    [Fact]
    public async Task ReemplazoValidoConservaNuevaLlaveSinFirmar()
    {
        using var carpeta = new CarpetaSintetica();
        using var primera = RSA.Create(2048);
        using var segunda = RSA.Create(2048);
        var ruta = await Guardar(carpeta.Storage, Certificado(primera));
        var anterior = await File.ReadAllTextAsync(ruta);
        var nuevo = Certificado(segunda);
        await Guardar(carpeta.Storage, nuevo);

        Assert.NotEqual(anterior, await File.ReadAllTextAsync(ruta));
        Assert.Equal(nuevo, await File.ReadAllTextAsync(ruta));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(ruta)!));
    }

    [Fact]
    public async Task CargaSinSeekValidaYLimiteNoDependeDeLength()
    {
        using var carpeta = new CarpetaSintetica();
        using var rsa = RSA.Create(2048);
        using var contenido = new SinSeek(Encoding.UTF8.GetBytes(Certificado(rsa)));
        var ruta = await carpeta.Storage.GuardarAsync("00", "0614-010123-101-0", contenido, "sintetico.crt");
        var anterior = await File.ReadAllBytesAsync(ruta);
        using var demasiado = new SinSeek(new byte[10 * 1024 * 1024 + 1]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => carpeta.Storage.GuardarAsync("00", Nit, demasiado, "sintetico.crt"));
        Assert.Equal(anterior, await File.ReadAllBytesAsync(ruta));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(ruta)!));
    }

    [Fact]
    public async Task CancelacionDeCargaConservaAnterior()
    {
        using var carpeta = new CarpetaSintetica();
        using var rsa = RSA.Create(2048);
        var valido = Certificado(rsa);
        var ruta = await Guardar(carpeta.Storage, valido);
        var anterior = await File.ReadAllBytesAsync(ruta);
        using var cancelacion = new CancellationTokenSource();
        using var stream = new SinSeek(Encoding.UTF8.GetBytes(valido), cancelacion);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => carpeta.Storage.GuardarAsync("00", Nit, stream, "sintetico.crt", cancelacion.Token));
        Assert.Equal(anterior, await File.ReadAllBytesAsync(ruta));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(ruta)!));
    }

    [Theory]
    [InlineData("{\"emisor\":{\"nit\":\"0614-010123-101-0\"}}")]
    [InlineData("{\"emisor\":{\"numDocumento\":\"06140101231010\"}}")]
    [InlineData("{\"donatario\":{\"numDocumento\":\"06140101231010\"}}")]
    public void NitDelCertificadoCoincideConEmisorODonatarioSinFirmar(string json)
        => InvocarNit(json, Nit);

    [Theory]
    [InlineData("{\"emisor\":{\"nit\":\"06140101239999\"}}")]
    [InlineData("{\"donatario\":{\"numDocumento\":\"06140101239999\"}}")]
    [InlineData("{\"emisor\":{\"nit\":123}}")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void NitAusenteODistintoImpideFirma(string json)
    {
        var error = Assert.Throws<TargetInvocationException>(() => InvocarNit(json, Nit));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    private static void InvocarNit(string json, string nit)
        => typeof(FirmadorDteService).GetMethod("ValidarNitDocumento", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { new CertificadoMh { nit = nit }, json });

    [Theory]
    [InlineData("http://transporte.invalid", "00", false)]
    [InlineData("http://transporte.invalid", "00", true)]
    [InlineData("http://127.0.0.1:8113", "00", false)]
    [InlineData("https://usuario:password@transporte.invalid", "00", false)]
    [InlineData("https://transporte.invalid/#fragmento", "00", false)]
    [InlineData("https://api.dtes.mh.gob.sv/fesv/recepciondte", "00", false)]
    [InlineData("https://apitest.dtes.mh.gob.sv/fesv/recepciondte", "01", false)]
    [InlineData("https://API.DTES.MH.GOB.SV./seguridad/auth", "00", false)]
    public void UrlInseguraOCruzadaEsRechazadaAntesDelEnvio(string url, string ambiente, bool loopback)
    {
        var error = Assert.Throws<TargetInvocationException>(() => Invocar<Uri>(null, typeof(HaciendaDteClient),
            "ValidarUrl", url, ambiente, loopback));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8113", "00", true)]
    [InlineData("http://[::1]:8113", "00", true)]
    [InlineData("http://localhost:8113", "01", true)]
    [InlineData("https://api.dtes.mh.gob.sv/fesv/recepciondte", "01", false)]
    [InlineData("https://apitest.dtes.mh.gob.sv/seguridad/auth", "00", false)]
    public void UrlCompatibleSeValidaSinRed(string url, string ambiente, bool loopback)
        => Assert.Equal(new Uri(url), Invocar<Uri>(null, typeof(HaciendaDteClient), "ValidarUrl", url, ambiente, loopback));

    [Theory]
    [InlineData("auth")]
    [InlineData("recepcion")]
    [InlineData("invalidacion")]
    [InlineData("contingencia")]
    public async Task EndpointOficialDeOtroAmbienteNuncaLlegaAlHandler(string operacion)
    {
        using var http = SinHttp();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = Configuracion("00");
        const string cruzada = "https://api.dtes.mh.gob.sv/ruta";
        config.Entidad.UrlAutenticacion = cruzada;
        config.Entidad.UrlRecepcion = cruzada;
        config.Entidad.UrlAnulacion = cruzada;
        config.Entidad.UrlContingencia = cruzada;
        var client = new HaciendaDteClient(http);
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            switch (operacion)
            {
                case "auth": await new TokenHaciendaService(cache, http).ObtenerAsync(config); break;
                case "recepcion": await client.EnviarDteAsync(config, "token", Jws, "01", 1, Codigo); break;
                case "invalidacion": await client.EnviarInvalidacionAsync(config, "token", Jws, Codigo); break;
                case "contingencia": await client.EnviarContingenciaAsync(config, "token", Jws); break;
            }
        });
        Assert.Equal(0, http.Llamadas);
    }

    [Theory]
    [InlineData("igual", true)]
    [InlineData("codigo", false)]
    [InlineData("ambiente", false)]
    [InlineData("json", false)]
    [InlineData("sin-json", false)]
    public void EnlaceTardioExigeCodigoAmbienteYJsonPersistidosIguales(string diferencia, bool esperado)
    {
        var request = new DteTransmisionRequest(Documento(), "01", Codigo, "control", 1, FacturaId: 23);
        Assert.Equal(esperado, Invocar<bool>(null, typeof(DteTransmisionService), "CoincideFacturaPersistida", request,
            diferencia == "codigo" ? "otro" : Codigo, diferencia == "ambiente" ? "01" : "00",
            diferencia == "sin-json" ? null : diferencia == "json" ? Documento(version: 2) : Documento()));
    }

    [Fact]
    public async Task FacturaYaVinculadaNoPuedeCambiarNiRequiereAccederBdParaRechazar()
    {
        using var http = SinHttp();
        var servicio = Servicio(http);
        var request = new DteTransmisionRequest(Documento(), "01", Codigo, "control", 1, FacturaId: 23);
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvocarAsyncSinResultado(servicio, "VincularFacturaAsync",
            request, new DteTransmision { FacturaId = 42 }, CancellationToken.None));
    }

    private static Task InvocarAsyncSinResultado(object instancia, string nombre, params object?[] args)
        => Invocar<Task>(instancia, instancia.GetType(), nombre, args);

    private static DteTransmisionService Servicio(HttpSimulado http, FirmadorSimulado? firmador = null,
        ITokenHaciendaService? token = null, IConfiguracionFeService? configuracion = null)
        => new(null!, configuracion ?? new ConfiguracionSimulada(), new JsonSimulado(), firmador ?? new FirmadorSimulado(),
            token ?? new TokenSimulado(), new HaciendaDteClient(http), http, NullLogger<DteTransmisionService>.Instance);

    private static Task<DteTransmisionResult> EvaluarEmision(DteTransmisionService servicio, ConfiguracionFeActiva config, CancellationToken ct = default)
        => Evaluar(servicio, () => InvocarAsync<HaciendaRespuesta>(servicio, "EjecutarEmisionAsync", config,
            Documento(config.Entidad.AmbienteFiscal), "01", 1, Codigo, "control", null, ct));

    private static Task<DteTransmisionResult> Evaluar(DteTransmisionService servicio, Func<Task<HaciendaRespuesta>> enviar)
        => InvocarAsync<DteTransmisionResult>(servicio, "EvaluarEnvioAsync", enviar, "00", 1);

    private static T Invocar<T>(object? instancia, Type tipo, string nombre, params object?[] args)
        => (T)tipo.GetMethod(nombre, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!.Invoke(instancia, args)!;

    private static Task<T> InvocarAsync<T>(object instancia, string nombre, params object?[] args)
        => Invocar<Task<T>>(instancia, instancia.GetType(), nombre, args);

    private static ConfiguracionFeActiva Configuracion(string ambiente = "00") => new(new ConfiguracionFacturacionElectronica
    {
        AmbienteFiscal = ambiente, Habilitado = true, NitEmisor = Nit, UsuarioHacienda = "usuario-sintetico",
        RutaCertificado = "ruta-no-leida-por-el-mock.crt", UrlFirmador = "https://transporte.invalid",
        UrlRecepcion = "https://transporte.invalid/recepcion", UrlAutenticacion = "https://transporte.invalid/auth",
        UrlAnulacion = "https://transporte.invalid/invalidacion", UrlContingencia = "https://transporte.invalid/contingencia",
        UrlApiExterna = "https://transporte.invalid"
    }, "credencial-sintetica", "password-sintetico");

    private static string Documento(string ambiente = "00", string tipo = "01", int version = 1)
        => JsonSerializer.Serialize(new { identificacion = new { ambiente, version, tipoDte = tipo, codigoGeneracion = Codigo } });

    private static string TokenJwt(int numero) => "e30." + Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
        new { exp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds(), jti = numero }))) + ".c2ln";
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static HttpResponseMessage Respuesta(int status, string body) => new((HttpStatusCode)status) { Content = new StringContent(body) };
    private static HttpSimulado SinHttp() => new((_, _) => throw new InvalidOperationException("No debe intentarse HTTP en esta prueba."));

    private static string Certificado(RSA rsa) => new XElement("CertificadoMH",
        new XElement("nit", Nit), new XElement("privateKey", new XElement("algorithm", "RSA"), new XElement("format", "PKCS#8"),
            new XElement("encodied", Convert.ToBase64String(rsa.ExportPkcs8PrivateKey())),
            new XElement("clave", Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes("password-sintetico")))))).ToString();

    private static async Task<string> Guardar(CertificadoStorageService storage, string xml)
    {
        using var contenido = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return await storage.GuardarAsync("00", Nit, contenido, "sintetico.crt");
    }

    private sealed class HttpSimulado : HttpMessageHandler, IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
        private int _llamadas;
        public int Llamadas => _llamadas;
        public HttpSimulado(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) => _responder = responder;
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _llamadas);
            Assert.Equal("transporte.invalid", request.RequestUri!.Host);
            return _responder(request, cancellationToken);
        }
    }

    private sealed class FirmadorSimulado : IFirmadorDteService
    {
        public int Llamadas { get; private set; }
        public Exception? Error { get; init; }
        public Task<string> FirmarAsync(string rutaCertificado, string password, string json, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Error is null ? Task.FromResult(Jws) : Task.FromException<string>(Error);
        }
        public void LimpiarCache(string? rutaCertificado = null) { }
    }

    private sealed class TokenSimulado : ITokenHaciendaService
    {
        public int Obtenciones { get; private set; }
        public int Invalidaciones { get; private set; }
        public int FallarEn { get; init; }
        public (string, string?) UltimaInvalidacion { get; private set; }
        public Task<string> ObtenerAsync(ConfiguracionFeActiva configuracion, CancellationToken cancellationToken = default)
        {
            Obtenciones++;
            return Obtenciones == FallarEn ? Task.FromException<string>(new HttpRequestException("Autenticación fallida"))
                : Task.FromResult("token-" + Obtenciones);
        }
        public void Invalidar(string ambienteFiscal, string? usuario = null)
        {
            Invalidaciones++;
            UltimaInvalidacion = (ambienteFiscal, usuario);
        }
    }

    private sealed class ConfiguracionSimulada : IConfiguracionFeService
    {
        public string? AmbienteSolicitado { get; private set; }
        public int ConsultasActiva { get; private set; }
        public Task<ConfiguracionFeActiva> ObtenerAsync(string ambienteFiscal, CancellationToken cancellationToken = default)
        {
            AmbienteSolicitado = ambienteFiscal;
            return Task.FromResult(Configuracion(ambienteFiscal));
        }
        public Task<ConfiguracionFeActiva> ObtenerActivaAsync(CancellationToken cancellationToken = default)
        {
            ConsultasActiva++;
            throw new InvalidOperationException("No seleccionar ambiente activo en reproceso.");
        }
        public Task<ConfiguracionFeActiva> ObtenerParaEdicionAsync(string ambienteFiscal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConfiguracionFeActiva>> ObtenerTodasAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task GuardarAsync(ConfiguracionFeInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SeleccionarAmbienteActivoAsync(string ambienteFiscal, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class JsonSimulado : IDteJsonFactory
    {
        public string ObtenerTipoCanonico(string tipoEntrada) => tipoEntrada;
        public void ValidarDocumento(string json, string ambienteFiscal, string? tipoDte = null)
        {
            using var documento = JsonDocument.Parse(json);
            Assert.Equal(ambienteFiscal, documento.RootElement.GetProperty("identificacion").GetProperty("ambiente").GetString());
        }
        public string CrearNota(string jsonDte, string tipoNota, string numeroControl, string codigoGeneracion, decimal monto, string? motivo, ConfiguracionFacturacionElectronica? configuracion = null) => throw new NotSupportedException();
        public string CrearContingencia(string jsonDte, int tipoContingencia, string motivo, string nombreResponsable, string nitResponsable, ConfiguracionFacturacionElectronica? configuracion = null) => throw new NotSupportedException();
        public string CrearInvalidacion(string jsonDte, string selloRecepcion, string motivoAnulacion, string nombreResponsable, string nitResponsable, ConfiguracionFacturacionElectronica? configuracion = null) => throw new NotSupportedException();
    }

    private sealed class CarpetaSintetica : IDisposable
    {
        private readonly string _ruta = Path.Combine(Path.GetTempPath(), "SRAUMOAR-transporte-tests-" + Guid.NewGuid().ToString("N"));
        public CertificadoStorageService Storage { get; }
        public CarpetaSintetica() => Storage = new CertificadoStorageService(new AmbienteWeb { ContentRootPath = _ruta });
        public void Dispose()
        {
            var absoluta = Path.GetFullPath(_ruta);
            if (!absoluta.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(absoluta).StartsWith("SRAUMOAR-transporte-tests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Ruta temporal de prueba inválida.");
            if (Directory.Exists(absoluta)) Directory.Delete(absoluta, recursive: true);
        }
    }

    private sealed class AmbienteWeb : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class SinSeek : MemoryStream
    {
        private readonly CancellationTokenSource? _cancelar;
        public SinSeek(byte[] bytes, CancellationTokenSource? cancelar = null) : base(bytes) => _cancelar = cancelar;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var leidos = await base.ReadAsync(buffer, cancellationToken);
            if (leidos == 0) _cancelar?.Cancel();
            return leidos;
        }
    }
}
