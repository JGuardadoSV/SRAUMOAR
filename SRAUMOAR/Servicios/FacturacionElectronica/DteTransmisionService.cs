using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface IDteTransmisionService
{
    Task<DteTransmisionResult> TransmitirAsync(DteTransmisionRequest request, CancellationToken cancellationToken = default);
    Task<DteEventoResult> TransmitirContingenciaAsync(int facturaId, string jsonEvento, CancellationToken cancellationToken = default);
    Task<DteEventoResult> TransmitirInvalidacionAsync(int facturaId, string jsonInvalidacion, CancellationToken cancellationToken = default);
}

public sealed class DteTransmisionService : IDteTransmisionService
{
    private readonly Contexto _contexto;
    private readonly IConfiguracionFeService _configuracionService;
    private readonly IDteJsonFactory _jsonFactory;
    private readonly IFirmadorDteService _firmador;
    private readonly ITokenHaciendaService _tokenService;
    private readonly IHaciendaDteClient _haciendaClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DteTransmisionService> _logger;

    public DteTransmisionService(
        Contexto contexto,
        IConfiguracionFeService configuracionService,
        IDteJsonFactory jsonFactory,
        IFirmadorDteService firmador,
        ITokenHaciendaService tokenService,
        IHaciendaDteClient haciendaClient,
        IHttpClientFactory httpClientFactory,
        ILogger<DteTransmisionService> logger)
    {
        _contexto = contexto;
        _configuracionService = configuracionService;
        _jsonFactory = jsonFactory;
        _firmador = firmador;
        _tokenService = tokenService;
        _haciendaClient = haciendaClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DteTransmisionResult> TransmitirAsync(DteTransmisionRequest request, CancellationToken cancellationToken = default)
    {
        var tipoSolicitado = request.TipoDte.Trim();
        var ambienteDocumento = ExtraerAmbienteDocumento(request.Json);
        var configuracion = tipoSolicitado is DteTipos.NotaCredito or DteTipos.NotaDebito
            ? await _configuracionService.ObtenerAsync(ambienteDocumento, cancellationToken)
            : await _configuracionService.ObtenerActivaAsync(cancellationToken);
        if (!configuracion.Entidad.Habilitado)
            throw new InvalidOperationException($"El ambiente fiscal {configuracion.Entidad.AmbienteFiscal} está deshabilitado.");
        _jsonFactory.ValidarDocumento(request.Json, configuracion.Entidad.AmbienteFiscal, request.TipoDte);
        var tipoDte = _jsonFactory.ObtenerTipoCanonico(request.TipoDte);
        var codigoJson = Newtonsoft.Json.Linq.JObject.Parse(request.Json)["identificacion"]?.Value<string>("codigoGeneracion");
        if (!string.Equals(codigoJson, request.CodigoGeneracion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El código de generación del request no coincide con el JSON DTE.");

        var existente = await _contexto.DteTransmisiones
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CodigoGeneracion == request.CodigoGeneracion
                && x.TipoOperacion == "EMISION"
                && x.AmbienteFiscal == configuracion.Entidad.AmbienteFiscal, cancellationToken);
        if (existente is not null)
        {
            return new DteTransmisionResult(
                existente.Estado == DteEstados.Aceptado,
                existente.Estado,
                existente.SelloRecepcion,
                existente.Respuesta ?? string.Empty,
                existente.Intentos,
                existente.AmbienteFiscal,
                existente.Error);
        }

        var transmision = new DteTransmision
        {
            FacturaId = request.FacturaId,
            TipoOperacion = "EMISION",
            AmbienteFiscal = configuracion.Entidad.AmbienteFiscal,
            CodigoGeneracion = request.CodigoGeneracion,
            JsonEnviado = request.Json,
            Estado = DteEstados.Preparado,
            FechaInicio = DateTime.UtcNow
        };
        _contexto.DteTransmisiones.Add(transmision);
        await _contexto.SaveChangesAsync(cancellationToken);

        try
        {
            var respuesta = await EjecutarEmisionAsync(configuracion, request.Json, tipoDte, request.Version, request.CodigoGeneracion, request.NumeroControl, request.CorreoReceptor, cancellationToken);
            transmision.Intentos = 1;
            transmision.SelloRecepcion = respuesta.SelloRecepcion;
            transmision.Respuesta = respuesta.Respuesta;
            transmision.Estado = respuesta.Exito ? DteEstados.Aceptado : DteEstados.Incierto;
            transmision.Error = respuesta.Error;
            transmision.FechaFin = DateTime.UtcNow;
            await _contexto.SaveChangesAsync(cancellationToken);
            return new DteTransmisionResult(respuesta.Exito, transmision.Estado, respuesta.SelloRecepcion, respuesta.Respuesta, 1, configuracion.Entidad.AmbienteFiscal, respuesta.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transmitiendo DTE {CodigoGeneracion} en ambiente fiscal {AmbienteFiscal}", request.CodigoGeneracion, configuracion.Entidad.AmbienteFiscal);
            transmision.Intentos = 1;
            transmision.Estado = DteEstados.Incierto;
            transmision.Error = ex.Message;
            transmision.FechaFin = DateTime.UtcNow;
            await _contexto.SaveChangesAsync(cancellationToken);
            return new DteTransmisionResult(false, DteEstados.Incierto, null, "", 1, configuracion.Entidad.AmbienteFiscal, ex.Message);
        }
    }

    public async Task<DteEventoResult> TransmitirContingenciaAsync(int facturaId, string jsonEvento, CancellationToken cancellationToken = default)
    {
        var ambienteFiscal = ExtraerAmbienteEvento(jsonEvento);
        var configuracion = await _configuracionService.ObtenerAsync(ambienteFiscal, cancellationToken);
        var codigoEvento = ExtraerCodigoEvento(jsonEvento);
        var existente = await ObtenerEventoExistenteAsync(codigoEvento, "CONTINGENCIA", ambienteFiscal, cancellationToken);
        if (existente is not null) return ConvertirResultadoEvento(existente);
        var evento = await RegistrarEventoAsync(facturaId, "CONTINGENCIA", codigoEvento, jsonEvento, ambienteFiscal, cancellationToken);
        try
        {
            var respuesta = await EjecutarEventoAsync(configuracion, jsonEvento, "CONTINGENCIA", cancellationToken);
            evento.SelloRecepcion = respuesta.SelloRecepcion;
            evento.Respuesta = respuesta.Respuesta;
            evento.Estado = respuesta.Exito ? DteEstados.Aceptado : DteEstados.Incierto;
            evento.Error = respuesta.Error;
            await _contexto.SaveChangesAsync(cancellationToken);
            return new DteEventoResult(respuesta.Exito, evento.Estado, respuesta.SelloRecepcion, respuesta.Respuesta, ambienteFiscal, respuesta.Error);
        }
        catch (Exception ex)
        {
            evento.Estado = DteEstados.Incierto;
            evento.Error = ex.Message;
            await _contexto.SaveChangesAsync(cancellationToken);
            return new DteEventoResult(false, evento.Estado, null, "", ambienteFiscal, ex.Message);
        }
    }

    public async Task<DteEventoResult> TransmitirInvalidacionAsync(int facturaId, string jsonInvalidacion, CancellationToken cancellationToken = default)
    {
        var ambienteFiscal = ExtraerAmbienteEvento(jsonInvalidacion);
        var configuracion = await _configuracionService.ObtenerAsync(ambienteFiscal, cancellationToken);
        var codigoEvento = ExtraerCodigoEvento(jsonInvalidacion);
        var existente = await ObtenerEventoExistenteAsync(codigoEvento, "INVALIDACION", ambienteFiscal, cancellationToken);
        if (existente is not null) return ConvertirResultadoEvento(existente);
        var evento = await RegistrarEventoAsync(facturaId, "INVALIDACION", codigoEvento, jsonInvalidacion, ambienteFiscal, cancellationToken);
        try
        {
            var respuesta = await EjecutarEventoAsync(configuracion, jsonInvalidacion, "INVALIDACION", cancellationToken);
            evento.SelloRecepcion = respuesta.SelloRecepcion;
            evento.Respuesta = respuesta.Respuesta;
            evento.Estado = respuesta.Exito ? DteEstados.Aceptado : DteEstados.Incierto;
            evento.Error = respuesta.Error;
            await _contexto.SaveChangesAsync(cancellationToken);
            return new DteEventoResult(respuesta.Exito, evento.Estado, respuesta.SelloRecepcion, respuesta.Respuesta, ambienteFiscal, respuesta.Error);
        }
        catch (Exception ex)
        {
            evento.Estado = DteEstados.Incierto;
            evento.Error = ex.Message;
            await _contexto.SaveChangesAsync(cancellationToken);
            return new DteEventoResult(false, evento.Estado, null, "", ambienteFiscal, ex.Message);
        }
    }

    private async Task<HaciendaRespuesta> EjecutarEmisionAsync(ConfiguracionFeActiva configuracion, string json, string tipoDte, int version, string codigoGeneracion, string numeroControl, string? correo, CancellationToken cancellationToken)
    {
        if (configuracion.Entidad.UsarApiExterna)
            return await EnviarApiExternaAsync(configuracion, json, tipoDte, version, codigoGeneracion, numeroControl, correo, cancellationToken);

        var jsonFirmado = await FirmarAsync(configuracion, json, cancellationToken);
        var token = await _tokenService.ObtenerAsync(configuracion, cancellationToken);
        return await _haciendaClient.EnviarDteAsync(configuracion, token, jsonFirmado, tipoDte, version, codigoGeneracion, cancellationToken);
    }

    private async Task<HaciendaRespuesta> EjecutarEventoAsync(ConfiguracionFeActiva configuracion, string json, string tipoEvento, CancellationToken cancellationToken)
    {
        if (configuracion.Entidad.UsarApiExterna)
            return await EnviarApiExternaEventoAsync(configuracion, json, tipoEvento, cancellationToken);

        var jsonFirmado = await FirmarAsync(configuracion, json, cancellationToken);
        var token = await _tokenService.ObtenerAsync(configuracion, cancellationToken);
        return tipoEvento == "INVALIDACION"
            ? await _haciendaClient.EnviarInvalidacionAsync(configuracion, token, jsonFirmado, ExtraerCodigoEvento(json), cancellationToken)
            : await _haciendaClient.EnviarContingenciaAsync(configuracion, token, jsonFirmado, cancellationToken);
    }

    private async Task<string> FirmarAsync(ConfiguracionFeActiva configuracion, string json, CancellationToken cancellationToken)
    {
        if (configuracion.Entidad.UsarFirmadorExterno)
        {
            if (string.IsNullOrWhiteSpace(configuracion.Entidad.UrlFirmador))
                throw new InvalidOperationException("El firmador externo no tiene URL configurada.");
            using var client = _httpClientFactory.CreateClient("FirmadorDte");
            using var response = await client.PostAsJsonAsync(configuracion.Entidad.UrlFirmador.TrimEnd('/') + "/firmardocumento/", new { nit = configuracion.Entidad.NitEmisor, activo = true, documento = json }, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("El firmador externo rechazó el documento.");
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("body", out var bodyNode) && bodyNode.TryGetProperty("documento", out var signed)
                ? signed.GetString() ?? throw new InvalidOperationException("El firmador externo no devolvió documento.")
                : document.RootElement.TryGetProperty("documento", out var rootSigned) ? rootSigned.GetString() ?? throw new InvalidOperationException("El firmador externo no devolvió documento.") : throw new InvalidOperationException("Respuesta inválida del firmador externo.");
        }

        if (string.IsNullOrWhiteSpace(configuracion.Entidad.RutaCertificado) || string.IsNullOrWhiteSpace(configuracion.PasswordCertificado))
            throw new InvalidOperationException("Falta certificado o contraseña para la firma interna.");
        return await _firmador.FirmarAsync(configuracion.Entidad.RutaCertificado, configuracion.PasswordCertificado, json, cancellationToken);
    }

    private async Task<HaciendaRespuesta> EnviarApiExternaAsync(ConfiguracionFeActiva configuracion, string json, string tipoDte, int version, string codigoGeneracion, string numeroControl, string? correo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuracion.Entidad.UrlApiExterna))
            throw new InvalidOperationException("La API externa está habilitada pero no tiene URL configurada.");
        using var client = _httpClientFactory.CreateClient("ApiExternaDte");
        var url = configuracion.Entidad.UrlApiExterna.TrimEnd('/') + "/api/procesar-dte";
        using var response = await client.PostAsJsonAsync(url, new
        {
            Usuario = configuracion.Entidad.UsuarioHacienda,
            Password = configuracion.CredencialApi,
            Ambiente = configuracion.Entidad.AmbienteFiscal,
            DteJson = json,
            Nit = configuracion.Entidad.NitEmisor,
            PasswordPrivado = configuracion.PasswordCertificado,
            TipoDte = tipoDte,
            CodigoGeneracion = codigoGeneracion,
            NumControl = numeroControl,
            VersionDte = version,
            CorreoCliente = correo,
            sincorreos = 1,
            UsarFirmadorInterno = !configuracion.Entidad.UsarFirmadorExterno
        }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return new HaciendaRespuesta(false, null, body, "La API externa rechazó el DTE.");
        return ParsearRespuesta(body);
    }

    private async Task<HaciendaRespuesta> EnviarApiExternaEventoAsync(ConfiguracionFeActiva configuracion, string json, string tipoEvento, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuracion.Entidad.UrlApiExterna))
            throw new InvalidOperationException("La API externa está habilitada pero no tiene URL configurada.");
        using var client = _httpClientFactory.CreateClient("ApiExternaDte");
        var endpoint = tipoEvento == "INVALIDACION" ? "anular-dte" : "contingencia";
        using var response = await client.PostAsJsonAsync(configuracion.Entidad.UrlApiExterna.TrimEnd('/') + "/api/" + endpoint, new
        {
            Usuario = configuracion.Entidad.UsuarioHacienda,
            Password = configuracion.CredencialApi,
            Ambiente = configuracion.Entidad.AmbienteFiscal,
            DteJson = json,
            Nit = configuracion.Entidad.NitEmisor,
            PasswordPrivado = configuracion.PasswordCertificado,
            UsarFirmadorInterno = !configuracion.Entidad.UsarFirmadorExterno
        }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return new HaciendaRespuesta(false, null, body, "La API externa rechazó el evento.");
        return ParsearRespuesta(body);
    }

    private async Task<DteEvento> RegistrarEventoAsync(int facturaId, string tipo, string codigo, string json, string ambiente, CancellationToken cancellationToken)
    {
        var evento = new DteEvento
        {
            FacturaId = facturaId,
            TipoEvento = tipo,
            AmbienteFiscal = ambiente,
            CodigoEvento = codigo,
            JsonEvento = json,
            Estado = DteEstados.Preparado
        };
        _contexto.DteEventos.Add(evento);
        await _contexto.SaveChangesAsync(cancellationToken);
        return evento;
    }

    private Task<DteEvento?> ObtenerEventoExistenteAsync(string codigo, string tipo, string ambiente, CancellationToken cancellationToken)
        => _contexto.DteEventos.AsNoTracking().SingleOrDefaultAsync(
            x => x.CodigoEvento == codigo && x.TipoEvento == tipo && x.AmbienteFiscal == ambiente,
            cancellationToken);

    private static DteEventoResult ConvertirResultadoEvento(DteEvento evento)
        => new(
            evento.Estado == DteEstados.Aceptado,
            evento.Estado,
            evento.SelloRecepcion,
            evento.Respuesta ?? string.Empty,
            evento.AmbienteFiscal,
            evento.Error);

    private static string ExtraerCodigoEvento(string json)
    {
        var codigo = Newtonsoft.Json.Linq.JObject.Parse(json)["identificacion"]?.Value<string>("codigoGeneracion");
        return string.IsNullOrWhiteSpace(codigo)
            ? throw new InvalidOperationException("El evento no contiene identificacion.codigoGeneracion.")
            : codigo;
    }

    private static string ExtraerAmbienteEvento(string json)
    {
        var ambiente = Newtonsoft.Json.Linq.JObject.Parse(json)["identificacion"]?.Value<string>("ambiente");
        return ConfiguracionFeService.NormalizarAmbiente(ambiente ?? string.Empty);
    }

    private static string ExtraerAmbienteDocumento(string json)
    {
        var ambiente = Newtonsoft.Json.Linq.JObject.Parse(json)["identificacion"]?.Value<string>("ambiente");
        return ConfiguracionFeService.NormalizarAmbiente(ambiente ?? string.Empty);
    }

    private static HaciendaRespuesta ParsearRespuesta(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var sello = document.RootElement.TryGetProperty("selloRecibido", out var direct) ? direct.GetString() : null;
            if (sello is null && document.RootElement.TryGetProperty("resultado", out var resultado) && resultado.TryGetProperty("selloRecibido", out var nested)) sello = nested.GetString();
            return new HaciendaRespuesta(!string.IsNullOrWhiteSpace(sello), sello, body, sello is null ? "No se recibió sello." : null);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new HaciendaRespuesta(false, null, body, ex.Message);
        }
    }
}
