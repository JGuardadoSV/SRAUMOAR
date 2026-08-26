using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface IDteEmailQueueService
{
    Task<ColaCorreoDte> EncolarDteAsync(Factura factura, string tipoDte, string json, string? sello, string destinatario, CancellationToken cancellationToken = default);
    Task<ColaCorreoDte> ReencolarDteAsync(Factura factura, string tipoDte, string json, string? sello, string destinatario, CancellationToken cancellationToken = default);
    Task<ColaCorreoDte> EncolarInvalidacionAsync(Factura factura, string jsonInvalidacion, string? sello, string destinatario, CancellationToken cancellationToken = default);
    Task ProcesarPendienteAsync(CancellationToken cancellationToken = default);
}

public sealed class DteEmailQueueService : IDteEmailQueueService
{
    private readonly Contexto _contexto;
    private readonly IDtePdfService _pdfService;
    private readonly IDteEmailSender _sender;
    private readonly ILogger<DteEmailQueueService> _logger;

    public DteEmailQueueService(Contexto contexto, IDtePdfService pdfService, IDteEmailSender sender, ILogger<DteEmailQueueService> logger)
    {
        _contexto = contexto;
        _pdfService = pdfService;
        _sender = sender;
        _logger = logger;
    }

    public Task<ColaCorreoDte> EncolarDteAsync(Factura factura, string tipoDte, string json, string? sello, string destinatario, CancellationToken cancellationToken = default)
        => EncolarInternoAsync(factura, tipoDte, json, sello, destinatario, false, false, cancellationToken);

    public Task<ColaCorreoDte> ReencolarDteAsync(Factura factura, string tipoDte, string json, string? sello, string destinatario, CancellationToken cancellationToken = default)
        => EncolarInternoAsync(factura, tipoDte, json, sello, destinatario, false, true, cancellationToken);

    public Task<ColaCorreoDte> EncolarInvalidacionAsync(Factura factura, string jsonInvalidacion, string? sello, string destinatario, CancellationToken cancellationToken = default)
        => EncolarInternoAsync(factura, DteTipos.Invalidacion, jsonInvalidacion, sello, destinatario, true, false, cancellationToken);

    public async Task ProcesarPendienteAsync(CancellationToken cancellationToken = default)
    {
        ColaCorreoDte? correo = null;
        await using (var transaccion = await _contexto.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            correo = await _contexto.ColaCorreosDte
                .Where(x => x.Estado == "Pendiente" && (x.ProximoIntento == null || x.ProximoIntento <= DateTime.UtcNow) && (x.BloqueadoHasta == null || x.BloqueadoHasta < DateTime.UtcNow))
                .OrderBy(x => x.FechaCreacion)
                .FirstOrDefaultAsync(cancellationToken);
            if (correo is null) return;
            correo.Estado = "Procesando";
            correo.BloqueadoHasta = DateTime.UtcNow.AddMinutes(10);
            correo.Intentos++;
            await _contexto.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
        }

        try
        {
            await _sender.SendAsync(correo!, cancellationToken);
            correo!.Estado = "Enviado";
            correo.ProcesadoEn = DateTime.UtcNow;
            correo.BloqueadoHasta = null;
            correo.UltimoError = null;
        }
        catch (Exception ex)
        {
            var max = await _contexto.ConfiguracionesSmtp.AsNoTracking().Where(x => x.Activo).Select(x => x.MaxReintentos).SingleOrDefaultAsync(cancellationToken);
            correo!.Estado = correo.Intentos >= (max <= 0 ? 5 : max) ? "Error" : "Pendiente";
            correo.ProximoIntento = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, correo.Intentos)));
            correo.BloqueadoHasta = null;
            correo.UltimoError = ex.Message;
            _logger.LogError(ex, "Error enviando correo DTE {CorreoId}", correo.Id);
        }
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private async Task<ColaCorreoDte> EncolarInternoAsync(Factura factura, string tipoDte, string json, string? sello, string destinatario, bool anulacion, bool forzarReenvio, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
            throw new ArgumentException("El destinatario es requerido.", nameof(destinatario));

        var codigo = ExtraerCodigo(json) ?? factura.CodigoGeneracion;
        var claveMaterial = $"{factura.FacturaId}:{tipoDte}:{codigo}:{sello}:{destinatario.Trim().ToUpperInvariant()}";
        var clave = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(claveMaterial)));
        var existente = await _contexto.ColaCorreosDte.SingleOrDefaultAsync(x => x.ClaveIdempotencia == clave, cancellationToken);
        if (existente is not null)
        {
            if (forzarReenvio && existente.Estado is not ("Pendiente" or "Procesando"))
            {
                existente.Estado = "Pendiente";
                existente.Intentos = 0;
                existente.ProximoIntento = DateTime.UtcNow;
                existente.BloqueadoHasta = null;
                existente.ProcesadoEn = null;
                existente.UltimoError = null;
                await _contexto.SaveChangesAsync(cancellationToken);
            }
            return existente;
        }

        var pdf = _pdfService.Generar(tipoDte, json, sello);
        var correo = new ColaCorreoDte
        {
            FacturaId = factura.FacturaId,
            TipoCorreo = anulacion ? "AnulacionDte" : "Dte",
            Destinatario = destinatario.Trim(),
            Asunto = anulacion ? $"Invalidación DTE {codigo}" : $"DTE {tipoDte.PadLeft(2, '0')} {codigo}",
            CuerpoHtml = anulacion ? "Se adjunta el documento de invalidación y su JSON." : "Se adjunta el Documento Tributario Electrónico y su JSON.",
            JsonAdjunto = json,
            NombreJson = $"{(anulacion ? "INVALIDACION" : "DTE-" + tipoDte.PadLeft(2, '0'))}-{codigo}.json",
            PdfAdjunto = pdf,
            NombrePdf = _pdfService.ObtenerNombre(tipoDte, codigo, anulacion),
            CodigoGeneracion = codigo,
            Sello = sello,
            Estado = "Pendiente",
            ClaveIdempotencia = clave,
            ProximoIntento = DateTime.UtcNow
        };
        _contexto.ColaCorreosDte.Add(correo);
        await _contexto.SaveChangesAsync(cancellationToken);
        return correo;
    }

    private static string? ExtraerCodigo(string json)
    {
        try
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            return root["identificacion"]?.Value<string>("codigoGeneracion") ?? root["documento"]?.Value<string>("codigoGeneracion");
        }
        catch (Newtonsoft.Json.JsonException) { return null; }
    }
}

public interface IDteEmailSender
{
    Task SendAsync(ColaCorreoDte correo, CancellationToken cancellationToken = default);
}

public sealed class DteEmailSender : IDteEmailSender
{
    private readonly ISmtpConfigurationService _configuracion;

    public DteEmailSender(ISmtpConfigurationService configuracion) => _configuracion = configuracion;

    public async Task SendAsync(ColaCorreoDte correo, CancellationToken cancellationToken = default)
    {
        var configuracion = await _configuracion.ObtenerAsync(cancellationToken);
        if (configuracion.Entidad is null) throw new InvalidOperationException("No existe configuración SMTP activa.");
        var password = await _configuracion.ObtenerPasswordAsync(cancellationToken);

        var mensaje = new MimeKit.MimeMessage();
        mensaje.From.Add(new MimeKit.MailboxAddress(configuracion.Entidad.NombreRemitente ?? configuracion.Entidad.EmailRemitente, configuracion.Entidad.EmailRemitente));
        mensaje.To.Add(MimeKit.MailboxAddress.Parse(correo.Destinatario));
        foreach (var copia in (configuracion.Entidad.CopiaOculta ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            mensaje.Bcc.Add(MimeKit.MailboxAddress.Parse(copia));
        mensaje.Subject = correo.Asunto;

        var cuerpo = new MimeKit.BodyBuilder { HtmlBody = correo.CuerpoHtml };
        cuerpo.Attachments.Add(correo.NombreJson, System.Text.Encoding.UTF8.GetBytes(correo.JsonAdjunto), new MimeKit.ContentType("application", "json"));
        if (correo.PdfAdjunto is { Length: > 0 } pdf && !string.IsNullOrWhiteSpace(correo.NombrePdf))
            cuerpo.Attachments.Add(correo.NombrePdf, pdf, new MimeKit.ContentType("application", "pdf"));
        mensaje.Body = cuerpo.ToMessageBody();

        using var cliente = new MailKit.Net.Smtp.SmtpClient();
        var socket = configuracion.Entidad.UsarSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : configuracion.Entidad.UsarTls ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.Auto;
        await cliente.ConnectAsync(configuracion.Entidad.Servidor, configuracion.Entidad.Puerto, socket, cancellationToken);
        if (!string.IsNullOrWhiteSpace(configuracion.Entidad.Usuario))
            await cliente.AuthenticateAsync(configuracion.Entidad.Usuario, password, cancellationToken);
        await cliente.SendAsync(mensaje, cancellationToken);
        await cliente.DisconnectAsync(true, cancellationToken);
    }
}

public sealed class DteEmailQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DteEmailQueueWorker> _logger;

    public DteEmailQueueWorker(IServiceScopeFactory scopeFactory, ILogger<DteEmailQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IDteEmailQueueService>().ProcesarPendienteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "La cola de correo DTE no pudo procesar el ciclo actual.");
            }
        }
    }
}
