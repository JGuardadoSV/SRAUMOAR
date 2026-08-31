using System.Data;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
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
        var candidato = await _contexto.ColaCorreosDte.AsNoTracking()
            .Where(FiltroElegibles(DateTime.UtcNow))
            .OrderBy(x => x.FechaCreacion).ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidato is null) return;

        // El lease permite recuperar un proceso caído; NO concede exclusión por sí solo.
        // El mismo bloqueo distribuido protege worker, recuperación y reenvío hasta
        // terminar SMTP y persistir el resultado, incluso si vence BloqueadoHasta.
        await using var exclusion = await AdquirirExclusionAsync(candidato.ClaveIdempotencia, cancellationToken);
        if (exclusion is null) return;

        ColaCorreoDte correo;
        await using (var transaccion = await _contexto.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            correo = await _contexto.ColaCorreosDte
                .SingleAsync(x => x.Id == candidato.Id, cancellationToken);
            // El contexto puede haber leído esta entidad antes de adquirir el bloqueo.
            await _contexto.Entry(correo).ReloadAsync(cancellationToken);
            if (!FiltroElegibles(DateTime.UtcNow).Compile()(correo)) return;
            if (correo.Estado == "Procesando")
                _logger.LogWarning("Recuperando lease vencido del correo DTE {CorreoId}; intentos previos {Intentos}", correo.Id, correo.Intentos);
            correo.Estado = "Procesando";
            correo.BloqueadoHasta = DateTime.UtcNow.AddMinutes(10);
            correo.Intentos++;
            await _contexto.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
        }

        try
        {
            using var envioCancelacion = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // Plazo inferior al lease; MailKit debe terminar/abortar antes de que
            // otra instancia pueda recuperar un lease tras perder la conexión SQL.
            envioCancelacion.CancelAfter(TimeSpan.FromMinutes(2));
            await _sender.SendAsync(correo, envioCancelacion.Token);
            correo.Estado = "Enviado";
            correo.ProcesadoEn = DateTime.UtcNow;
            correo.BloqueadoHasta = null;
            // Conservar el último error como evidencia de los intentos anteriores.
        }
        catch (Exception ex)
        {
            var max = await _contexto.ConfiguracionesSmtp.AsNoTracking().Where(x => x.Activo).Select(x => x.MaxReintentos).SingleOrDefaultAsync(CancellationToken.None);
            correo.Estado = correo.Intentos >= (max <= 0 ? 5 : max) ? "Error" : "Pendiente";
            correo.ProximoIntento = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, correo.Intentos)));
            correo.BloqueadoHasta = null;
            correo.UltimoError = ex.Message;
            _logger.LogError(ex, "Error enviando correo DTE {CorreoId}", correo.Id);
        }
        // Un cierre solicitado después de SMTP no debe impedir guardar su resultado.
        await _contexto.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<ColaCorreoDte> EncolarInternoAsync(Factura factura, string tipoDte, string json, string? sello, string destinatario, bool anulacion, bool forzarReenvio, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
            throw new ArgumentException("El destinatario es requerido.", nameof(destinatario));

        var codigo = ExtraerCodigo(json) ?? factura.CodigoGeneracion;
        var claveMaterial = $"{factura.FacturaId}:{tipoDte}:{codigo}:{sello}:{destinatario.Trim().ToUpperInvariant()}";
        var clave = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(claveMaterial)));
        await using var exclusion = await AdquirirExclusionAsync(clave, cancellationToken);
        if (exclusion is null)
            throw new InvalidOperationException("Este correo está siendo procesado. Espere a que termine antes de solicitar otro envío.");

        var prefijoReenvio = clave + ":";
        var existente = await _contexto.ColaCorreosDte
            .Where(x => x.ClaveIdempotencia == clave || x.ClaveIdempotencia.StartsWith(prefijoReenvio))
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (existente is not null)
        {
            await _contexto.Entry(existente).ReloadAsync(cancellationToken);
            if (forzarReenvio && RecuperarLeaseVencido(existente, DateTime.UtcNow))
            {
                await _contexto.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Lease recuperado por reenvío del correo DTE {CorreoId}", existente.Id);
            }
            if (!forzarReenvio || existente.Estado is not ("Enviado" or "Error"))
                return existente;

            // Un reenvío terminado es una nueva entrega: el registro anterior y toda
            // su auditoría permanecen intactos. La familia comparte el mismo bloqueo.
            clave = prefijoReenvio + Guid.NewGuid().ToString("N");
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
        if (existente is not null)
            _logger.LogInformation("Reenvío DTE {CorreoId} creado desde {CorreoAnteriorId}", correo.Id, existente.Id);
        return correo;
    }

    private static Expression<Func<ColaCorreoDte, bool>> FiltroElegibles(DateTime ahora)
        => x => (x.BloqueadoHasta == null || x.BloqueadoHasta <= ahora)
            && (x.Estado == "Procesando"
                || (x.Estado == "Pendiente" && (x.ProximoIntento == null || x.ProximoIntento <= ahora)));

    // Solo llamar después de adquirir la exclusión compartida con el worker.
    private static bool RecuperarLeaseVencido(ColaCorreoDte correo, DateTime ahora)
    {
        if (correo.Estado != "Procesando" || !FiltroElegibles(ahora).Compile()(correo)) return false;
        correo.Estado = "Pendiente";
        correo.ProximoIntento = ahora;
        correo.BloqueadoHasta = null;
        return true;
    }

    private static string RecursoExclusion(string clave)
        => "SRAUMOAR:DteEmail:" + clave.Split(':', 2)[0];

    private async Task<SqlConnection?> AdquirirExclusionAsync(string clave, CancellationToken cancellationToken)
    {
        // Conexión dedicada: su transacción solo mantiene el application lock, no
        // bloquea filas durante SMTP. Las transacciones de trabajo se confirman aparte.
        // Dispose cierra/rollback y libera el lock incluso ante excepción/cancelación.
        // Usar las opciones de EF, no la cadena de una conexión ya abierta (SqlClient
        // puede haber ocultado su contraseña con Persist Security Info=false).
        var opciones = new SqlConnectionStringBuilder(_contexto.Database.GetConnectionString())
        {
            Pooling = false,
            Enlist = false,
            ConnectRetryCount = 0
        };
        var conexion = new SqlConnection(opciones.ConnectionString);
        try
        {
            await conexion.OpenAsync(cancellationToken);
            using var comando = conexion.CreateCommand();
            comando.CommandText = """
                BEGIN TRANSACTION;
                DECLARE @resultado int;
                EXEC @resultado = sys.sp_getapplock @Resource = @recurso,
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;
                SELECT @resultado;
                """;
            comando.Parameters.AddWithValue("@recurso", RecursoExclusion(clave));
            var resultado = Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken));
            if (resultado >= 0) return conexion;
            await conexion.DisposeAsync();
            if (resultado == -1) return null; // Otra instancia posee el envío.
            throw new InvalidOperationException($"No se pudo adquirir la exclusión del correo DTE ({resultado}).");
        }
        catch
        {
            await conexion.DisposeAsync();
            throw;
        }
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
