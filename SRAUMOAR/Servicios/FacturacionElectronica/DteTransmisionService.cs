using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
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
        var ambienteDocumento = ExtraerAmbienteDocumento(request.Json);
        var transmision = await _contexto.DteTransmisiones
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CodigoGeneracion == request.CodigoGeneracion
                && x.TipoOperacion == "EMISION"
                && x.AmbienteFiscal == ambienteDocumento, cancellationToken);
        if (transmision is null)
        {
            // INSERT parametrizado y actualizaciones por clave: nunca guardar ni desconectar
            // las facturas/reservas pendientes que pertenecen al contexto del llamador.
            try
            {
                await _contexto.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO [DteTransmisiones]
                        ([FacturaId], [TipoOperacion], [AmbienteFiscal], [CodigoGeneracion], [JsonEnviado], [Estado], [FechaInicio], [Intentos])
                    VALUES ({request.FacturaId}, {"EMISION"}, {ambienteDocumento}, {request.CodigoGeneracion},
                        {request.Json}, {DteEstados.Preparado}, {DateTime.UtcNow}, {0})", cancellationToken);
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627) { /* Otro proceso registró la misma clave. */ }
            transmision = await _contexto.DteTransmisiones.AsNoTracking().SingleAsync(x =>
                x.CodigoGeneracion == request.CodigoGeneracion && x.TipoOperacion == "EMISION" &&
                x.AmbienteFiscal == ambienteDocumento, cancellationToken);
        }

        if (!PuedeReintentar(transmision.Estado)) return ConvertirResultado(transmision);
        var reclamada = await _contexto.DteTransmisiones.Where(x => x.Id == transmision.Id &&
                x.Estado == DteEstados.Preparado && x.Intentos == transmision.Intentos)
            .ExecuteUpdateAsync(x => x
                .SetProperty(t => t.Estado, DteEstados.Enviado)
                .SetProperty(t => t.FechaInicio, DateTime.UtcNow)
                .SetProperty(t => t.Intentos, t => t.Intentos + 1), cancellationToken);
        if (reclamada == 0)
        {
            transmision = await _contexto.DteTransmisiones.AsNoTracking().SingleAsync(x => x.Id == transmision.Id, cancellationToken);
            return ConvertirResultado(transmision);
        }
        transmision.Intentos++;
        var resultado = await EvaluarEnvioAsync(() => PrepararYEnviarDocumentoAsync(request, transmision, cancellationToken),
            ambienteDocumento, transmision.Intentos);
        // Una cancelación del request no debe impedir liberar una reserva PRE ni registrar incertidumbre POST.
        using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _contexto.DteTransmisiones.Where(x => x.Id == transmision.Id && x.Estado == DteEstados.Enviado && x.Intentos == transmision.Intentos)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.Estado, resultado.Estado)
                .SetProperty(t => t.SelloRecepcion, resultado.SelloRecepcion)
                .SetProperty(t => t.Respuesta, resultado.Respuesta).SetProperty(t => t.Error, resultado.Error)
                .SetProperty(t => t.FechaFin, DateTime.UtcNow), cierre.Token);
        return resultado;
    }

    public Task<DteEventoResult> TransmitirContingenciaAsync(int facturaId, string jsonEvento, CancellationToken cancellationToken = default)
        => TransmitirEventoAsync(facturaId, jsonEvento, "CONTINGENCIA", cancellationToken);

    public Task<DteEventoResult> TransmitirInvalidacionAsync(int facturaId, string jsonInvalidacion, CancellationToken cancellationToken = default)
        => TransmitirEventoAsync(facturaId, jsonInvalidacion, "INVALIDACION", cancellationToken);

    private async Task<DteEventoResult> TransmitirEventoAsync(int facturaId, string json, string tipo, CancellationToken cancellationToken)
    {
        var ambienteFiscal = ExtraerAmbienteEvento(json);
        var codigoEvento = ExtraerCodigoEvento(json);
        var evento = await ObtenerEventoExistenteAsync(codigoEvento, tipo, ambienteFiscal, cancellationToken)
            ?? await RegistrarEventoAsync(facturaId, tipo, codigoEvento, json, ambienteFiscal, cancellationToken);
        if (!PuedeReintentar(evento.Estado)) return ConvertirResultadoEvento(evento);
        var reclamada = await _contexto.DteEventos.Where(x => x.Id == evento.Id && x.Estado == DteEstados.Preparado)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.Estado, DteEstados.Enviado), cancellationToken);
        if (reclamada == 0)
            return ConvertirResultadoEvento((await ObtenerEventoExistenteAsync(codigoEvento, tipo, ambienteFiscal, cancellationToken))!);
        var resultado = await EvaluarEnvioAsync(() => PrepararYEnviarEventoAsync(facturaId, json, tipo, evento, cancellationToken), ambienteFiscal, 1);
        using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _contexto.DteEventos.Where(x => x.Id == evento.Id && x.Estado == DteEstados.Enviado)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.Estado, resultado.Estado)
                .SetProperty(t => t.SelloRecepcion, resultado.SelloRecepcion)
                .SetProperty(t => t.Respuesta, resultado.Respuesta).SetProperty(t => t.Error, resultado.Error), cierre.Token);
        return new(resultado.Aceptado, resultado.Estado, resultado.SelloRecepcion, resultado.Respuesta, ambienteFiscal, resultado.Error);
    }

    private async Task<HaciendaRespuesta> PrepararYEnviarDocumentoAsync(DteTransmisionRequest request, DteTransmision transmision, CancellationToken cancellationToken)
    {
        ConfiguracionFeActiva configuracion;
        string tipoDte;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Newtonsoft.Json.Linq.JToken.DeepEquals(
                Newtonsoft.Json.Linq.JObject.Parse(transmision.JsonEnviado), Newtonsoft.Json.Linq.JObject.Parse(request.Json)))
                throw new InvalidOperationException("El reproceso debe conservar la factura y el JSON originalmente reservados.");
            configuracion = await ObtenerConfiguracionDocumentoAsync(request, cancellationToken);
            tipoDte = _jsonFactory.ObtenerTipoCanonico(request.TipoDte);
            await VincularFacturaAsync(request, transmision, cancellationToken);
        }
        catch (Exception ex) { throw new DteEnvioNoRealizadoException(ex); }
        return await EjecutarEmisionAsync(configuracion, request.Json, tipoDte, request.Version, request.CodigoGeneracion,
            request.NumeroControl, request.CorreoReceptor, cancellationToken);
    }

    private async Task VincularFacturaAsync(DteTransmisionRequest request, DteTransmision transmision, CancellationToken cancellationToken)
    {
        if (transmision.FacturaId == request.FacturaId) return;
        if (transmision.FacturaId.HasValue || !request.FacturaId.HasValue)
            throw new InvalidOperationException("No se permite cambiar la factura vinculada a la transmisión.");
        // La primera venta puede transmitirse antes de crear Factura. El enlace tardío
        // requiere identidad fiscal y documento persistido iguales, no sólo el id recibido.
        var factura = await _contexto.Facturas.AsNoTracking()
            .Where(x => x.FacturaId == request.FacturaId.Value)
            .Select(x => new { x.CodigoGeneracion, x.AmbienteFiscal, x.JsonDte })
            .SingleOrDefaultAsync(cancellationToken);
        if (factura is null || !CoincideFacturaPersistida(request, factura.CodigoGeneracion, factura.AmbienteFiscal, factura.JsonDte))
            throw new InvalidOperationException("La factura persistida no coincide con código, ambiente y JSON de la transmisión.");
        var vinculada = await _contexto.DteTransmisiones.Where(x => x.Id == transmision.Id && x.FacturaId == null &&
                x.Estado == DteEstados.Enviado && x.Intentos == transmision.Intentos)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.FacturaId, request.FacturaId), cancellationToken);
        if (vinculada != 1) throw new InvalidOperationException("No se pudo vincular la factura a la reserva exclusiva.");
        transmision.FacturaId = request.FacturaId;
    }

    private static bool CoincideFacturaPersistida(DteTransmisionRequest request, string? codigo, string? ambiente, string? json)
        => string.Equals(codigo, request.CodigoGeneracion, StringComparison.OrdinalIgnoreCase) &&
           ambiente == ExtraerAmbienteDocumento(request.Json) && !string.IsNullOrWhiteSpace(json) &&
           Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JObject.Parse(json), Newtonsoft.Json.Linq.JObject.Parse(request.Json));

    private async Task<HaciendaRespuesta> PrepararYEnviarEventoAsync(int facturaId, string json, string tipo, DteEvento evento, CancellationToken cancellationToken)
    {
        ConfiguracionFeActiva configuracion;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (evento.FacturaId != facturaId || !Newtonsoft.Json.Linq.JToken.DeepEquals(
                Newtonsoft.Json.Linq.JObject.Parse(evento.JsonEvento), Newtonsoft.Json.Linq.JObject.Parse(json)))
                throw new InvalidOperationException("El reproceso debe conservar la factura y el JSON del evento reservado.");
            configuracion = await _configuracionService.ObtenerAsync(ExtraerAmbienteEvento(json), cancellationToken);
        }
        catch (Exception ex) { throw new DteEnvioNoRealizadoException(ex); }
        return await EjecutarEventoAsync(configuracion, json, tipo, cancellationToken);
    }

    private async Task<HaciendaRespuesta> EjecutarEmisionAsync(ConfiguracionFeActiva configuracion, string json, string tipoDte, int version, string codigoGeneracion, string numeroControl, string? correo, CancellationToken cancellationToken)
    {
        if (configuracion.Entidad.UsarApiExterna)
            return await EnviarApiExternaAsync(configuracion, json, tipoDte, version, codigoGeneracion, numeroControl, correo, cancellationToken);

        return await EnviarFirmadoAsync(configuracion, json,
            (token, firmado) => _haciendaClient.EnviarDteAsync(configuracion, token, firmado, tipoDte, version, codigoGeneracion, cancellationToken), cancellationToken);
    }

    private async Task<HaciendaRespuesta> EjecutarEventoAsync(ConfiguracionFeActiva configuracion, string json, string tipoEvento, CancellationToken cancellationToken)
    {
        try
        {
            var version = ExtraerVersion(json);
            ValidarConfiguracion(configuracion, ExtraerAmbienteEvento(json));
            DteSchemaValidator.Validar(json, tipoEvento.ToLowerInvariant(), version);
        }
        catch (Exception ex) { throw new DteEnvioNoRealizadoException(ex); }
        if (configuracion.Entidad.UsarApiExterna)
            return await EnviarApiExternaEventoAsync(configuracion, json, tipoEvento, cancellationToken);

        return await EnviarFirmadoAsync(configuracion, json, (token, firmado) => tipoEvento == "INVALIDACION"
            ? _haciendaClient.EnviarInvalidacionAsync(configuracion, token, firmado, ExtraerCodigoEvento(json), cancellationToken)
            : _haciendaClient.EnviarContingenciaAsync(configuracion, token, firmado, cancellationToken), cancellationToken);
    }

    private async Task<HaciendaRespuesta> EnviarFirmadoAsync(ConfiguracionFeActiva configuracion, string json,
        Func<string, string, Task<HaciendaRespuesta>> enviar, CancellationToken cancellationToken)
    {
        string firmado, token;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            firmado = await FirmarAsync(configuracion, json, cancellationToken);
            token = await _tokenService.ObtenerAsync(configuracion, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex) { throw new DteEnvioNoRealizadoException(ex); }

        try { return await enviar(token, firmado); }
        catch (HaciendaNoAutorizadoException)
        {
            // Sólo un 401 explícito permite repetir el POST; nunca errores de red, 5xx o respuestas ambiguas.
            try
            {
                if (_tokenService is TokenHaciendaService servicio)
                    token = await servicio.RenovarTrasRechazoAsync(configuracion, token, cancellationToken);
                else
                {
                    _tokenService.Invalidar(configuracion.Entidad.AmbienteFiscal, configuracion.Entidad.UsuarioHacienda);
                    token = await _tokenService.ObtenerAsync(configuracion, cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex) { throw new DteEnvioNoRealizadoException(ex); }
            try { return await enviar(token, firmado); }
            catch (HaciendaNoAutorizadoException ex) { throw new DteEnvioNoRealizadoException(ex); }
        }
    }

    private async Task<string> FirmarAsync(ConfiguracionFeActiva configuracion, string json, CancellationToken cancellationToken)
    {
        if (configuracion.Entidad.UsarFirmadorExterno)
        {
            if (string.IsNullOrWhiteSpace(configuracion.Entidad.UrlFirmador))
                throw new InvalidOperationException("El firmador externo no tiene URL configurada.");
            if (string.IsNullOrWhiteSpace(configuracion.PasswordCertificado))
                throw new InvalidOperationException("Falta contraseña de la clave privada para el firmador externo.");
            using var dte = JsonDocument.Parse(json);
            if (dte.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("dteJson debe ser un objeto JSON.");
            using var client = _httpClientFactory.CreateClient("FirmadorDte");
            using var response = await client.PostAsJsonAsync(HaciendaDteClient.ValidarUrl(configuracion.Entidad.UrlFirmador.TrimEnd('/') + "/firmardocumento/",
                configuracion.Entidad.AmbienteFiscal, permitirHttpLoopback: true),
                new { nit = configuracion.Entidad.NitEmisor, activo = true, passwordPri = configuracion.PasswordCertificado, dteJson = dte.RootElement }, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("El firmador externo rechazó el documento.");
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                (document.RootElement.TryGetProperty("status", out var status) &&
                 (status.ValueKind != JsonValueKind.String || !string.Equals(status.GetString(), "OK", StringComparison.OrdinalIgnoreCase))) ||
                !document.RootElement.TryGetProperty("body", out var bodyNode) || bodyNode.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Respuesta inválida del firmador externo: se esperaba body como JWS.");
            var firmado = bodyNode.GetString()!.Trim();
            var partes = firmado.Split('.');
            if (partes.Length != 3 || partes.Any(p => p.Length == 0 || p.Any(c =>
                !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))))
                throw new InvalidOperationException("El firmador externo no devolvió un JWS compacto válido.");
            return firmado;
        }

        if (string.IsNullOrWhiteSpace(configuracion.Entidad.RutaCertificado) || string.IsNullOrWhiteSpace(configuracion.PasswordCertificado))
            throw new InvalidOperationException("Falta certificado o contraseña para la firma interna.");
        return await _firmador.FirmarAsync(configuracion.Entidad.RutaCertificado, configuracion.PasswordCertificado, json, cancellationToken);
    }

    private async Task<HaciendaRespuesta> EnviarApiExternaAsync(ConfiguracionFeActiva configuracion, string json, string tipoDte, int version, string codigoGeneracion, string numeroControl, string? correo, CancellationToken cancellationToken)
    {
        return await PostApiExternaAsync(configuracion, "procesar-dte", new
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
    }

    private async Task<HaciendaRespuesta> EnviarApiExternaEventoAsync(ConfiguracionFeActiva configuracion, string json, string tipoEvento, CancellationToken cancellationToken)
    {
        var endpoint = tipoEvento == "INVALIDACION" ? "anular-dte" : "contingencia";
        return await PostApiExternaAsync(configuracion, endpoint, new
        {
            Usuario = configuracion.Entidad.UsuarioHacienda,
            Password = configuracion.CredencialApi,
            Ambiente = configuracion.Entidad.AmbienteFiscal,
            DteJson = json,
            Nit = configuracion.Entidad.NitEmisor,
            PasswordPrivado = configuracion.PasswordCertificado,
            UsarFirmadorInterno = !configuracion.Entidad.UsarFirmadorExterno
        }, cancellationToken);
    }

    private async Task<HaciendaRespuesta> PostApiExternaAsync(ConfiguracionFeActiva configuracion, string endpoint, object payload, CancellationToken cancellationToken)
    {
        HttpClient? client = null;
        HttpRequestMessage? request = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(configuracion.Entidad.UrlApiExterna))
                throw new InvalidOperationException("La API externa está habilitada pero no tiene URL configurada.");
            var url = HaciendaDteClient.ValidarUrl(configuracion.Entidad.UrlApiExterna.TrimEnd('/') + "/api/" + endpoint, configuracion.Entidad.AmbienteFiscal);
            client = _httpClientFactory.CreateClient("ApiExternaDte");
            request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
            };
        }
        catch (Exception ex)
        {
            request?.Dispose(); client?.Dispose();
            throw new DteEnvioNoRealizadoException(ex);
        }
        using var clientLifetime = client;
        using var requestLifetime = request;
        using var envio = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        envio.CancelAfter(client.Timeout);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, envio.Token);
        var body = await response.Content.ReadAsStringAsync(envio.Token);
        if (!response.IsSuccessStatusCode)
            return new(false, null, body, "La API externa no confirmó la recepción.", HaciendaDteClient.EsRechazoFiscalExplicito(body));
        return ParsearRespuesta(body);
    }

    private async Task<DteEvento> RegistrarEventoAsync(int facturaId, string tipo, string codigo, string json, string ambiente, CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO [DteEventos]
                    ([FacturaId], [TipoEvento], [AmbienteFiscal], [CodigoEvento], [JsonEvento], [Estado], [FechaCreacion])
                VALUES ({facturaId}, {tipo}, {ambiente}, {codigo}, {json}, {DteEstados.Preparado}, {DateTime.UtcNow})", cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { }
        return (await ObtenerEventoExistenteAsync(codigo, tipo, ambiente, cancellationToken))!;
    }

    private static bool PuedeReintentar(string estado) => estado == DteEstados.Preparado;

    private static DteTransmisionResult ConvertirResultado(DteTransmision transmision)
        => new(transmision.Estado == DteEstados.Aceptado, transmision.Estado, transmision.SelloRecepcion,
            transmision.Respuesta ?? string.Empty, transmision.Intentos, transmision.AmbienteFiscal, transmision.Error);

    private async Task<DteTransmisionResult> EvaluarEnvioAsync(Func<Task<HaciendaRespuesta>> enviar, string ambiente, int intentos)
    {
        try
        {
            var respuesta = await enviar();
            var estado = respuesta.Exito ? DteEstados.Aceptado : respuesta.RechazoFiscal ? DteEstados.Rechazado : DteEstados.Incierto;
            return new(respuesta.Exito, estado, respuesta.SelloRecepcion, respuesta.Respuesta, intentos, ambiente, respuesta.Error);
        }
        catch (Exception ex)
        {
            var estado = ex is DteEnvioNoRealizadoException ? DteEstados.Preparado : DteEstados.Incierto;
            _logger.LogWarning("Transporte DTE finalizó en {Estado} en ambiente {AmbienteFiscal}", estado, ambiente);
            return new(false, estado, null, string.Empty, intentos, ambiente, ex.Message);
        }
    }

    private async Task<ConfiguracionFeActiva> ObtenerConfiguracionDocumentoAsync(DteTransmisionRequest request, CancellationToken cancellationToken)
    {
        ValidarIdentificacion(request);
        var ambiente = ExtraerAmbienteDocumento(request.Json);
        var configuracion = await _configuracionService.ObtenerAsync(ambiente, cancellationToken);
        ValidarConfiguracion(configuracion, ambiente);
        _jsonFactory.ValidarDocumento(request.Json, ambiente, request.TipoDte);
        return configuracion;
    }

    private static void ValidarConfiguracion(ConfiguracionFeActiva configuracion, string ambiente)
    {
        if (!configuracion.Entidad.Habilitado || configuracion.Entidad.AmbienteFiscal != ambiente)
            throw new InvalidOperationException($"No hay configuración habilitada para el ambiente del JSON {ambiente}.");
    }

    private static void ValidarIdentificacion(DteTransmisionRequest request)
    {
        var identificacion = Newtonsoft.Json.Linq.JObject.Parse(request.Json)["identificacion"];
        var codigo = identificacion?.Value<string>("codigoGeneracion");
        if (!string.Equals(codigo, request.CodigoGeneracion, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(codigo))
            throw new InvalidOperationException("El código de generación del request no coincide con el JSON DTE.");
        if (ExtraerVersion(request.Json) != request.Version)
            throw new InvalidOperationException("La versión de transmisión no coincide con identificacion.version.");
    }

    private static int ExtraerVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("identificacion", out var identificacion) || identificacion.ValueKind != JsonValueKind.Object ||
            !identificacion.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var numero) || numero <= 0)
            throw new InvalidOperationException("El JSON no contiene identificacion.version entera positiva.");
        return numero;
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
            var root = document.RootElement;
            string? sello = null;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("selloRecibido", out var direct) && direct.ValueKind == JsonValueKind.String) sello = direct.GetString();
                if (string.IsNullOrWhiteSpace(sello) && root.TryGetProperty("resultado", out var resultado) && resultado.ValueKind == JsonValueKind.Object &&
                    resultado.TryGetProperty("selloRecibido", out var nested) && nested.ValueKind == JsonValueKind.String) sello = nested.GetString();
            }
            return new HaciendaRespuesta(!string.IsNullOrWhiteSpace(sello), sello, body, string.IsNullOrWhiteSpace(sello) ? "No se recibió sello." : null,
                string.IsNullOrWhiteSpace(sello) && HaciendaDteClient.EsRechazoFiscalExplicito(body));
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new HaciendaRespuesta(false, null, body, ex.Message);
        }
    }
}
