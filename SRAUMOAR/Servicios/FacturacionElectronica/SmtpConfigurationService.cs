using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Smtp;
using MailKit.Security;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public sealed record SmtpConfigurationInput(string Servidor, int Puerto, string? Usuario, string? Password, string EmailRemitente, string? NombreRemitente, string? CopiaOculta, bool UsarSsl, bool UsarTls, int MaxReintentos);
public sealed record SmtpConfigurationView(ConfiguracionSmtp? Entidad, bool TienePassword);

public interface ISmtpConfigurationService
{
    Task<SmtpConfigurationView> ObtenerAsync(CancellationToken cancellationToken = default);
    Task GuardarAsync(SmtpConfigurationInput input, CancellationToken cancellationToken = default);
    Task<string?> ObtenerPasswordAsync(CancellationToken cancellationToken = default);
    Task ProbarAsync(CancellationToken cancellationToken = default);
    Task EnviarPruebaAsync(CancellationToken cancellationToken = default);
}

public sealed class SmtpConfigurationService : ISmtpConfigurationService
{
    private readonly Contexto _contexto;
    private readonly IDataProtector _protector;

    public SmtpConfigurationService(Contexto contexto, IDataProtectionProvider provider)
    {
        _contexto = contexto;
        _protector = provider.CreateProtector("SRAUMOAR.Smtp.Secret.v1");
    }

    public async Task<SmtpConfigurationView> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var entidad = await _contexto.ConfiguracionesSmtp.AsNoTracking().SingleOrDefaultAsync(x => x.Activo, cancellationToken);
        return new SmtpConfigurationView(entidad, !string.IsNullOrWhiteSpace(entidad?.PasswordCifrada));
    }

    public async Task GuardarAsync(SmtpConfigurationInput input, CancellationToken cancellationToken = default)
    {
        var entidad = await _contexto.ConfiguracionesSmtp.SingleOrDefaultAsync(x => x.Activo, cancellationToken);
        if (entidad is null)
        {
            entidad = new ConfiguracionSmtp { Activo = true };
            _contexto.ConfiguracionesSmtp.Add(entidad);
        }
        entidad.Servidor = input.Servidor.Trim();
        entidad.Puerto = Math.Clamp(input.Puerto, 1, 65535);
        entidad.Usuario = input.Usuario?.Trim();
        if (!string.IsNullOrWhiteSpace(input.Password)) entidad.PasswordCifrada = _protector.Protect(input.Password);
        entidad.EmailRemitente = input.EmailRemitente.Trim();
        entidad.NombreRemitente = input.NombreRemitente?.Trim();
        entidad.CopiaOculta = input.CopiaOculta?.Trim();
        entidad.UsarSsl = input.UsarSsl;
        entidad.UsarTls = input.UsarTls;
        entidad.MaxReintentos = Math.Clamp(input.MaxReintentos, 1, 10);
        entidad.FechaActualizacion = DateTime.UtcNow;
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> ObtenerPasswordAsync(CancellationToken cancellationToken = default)
    {
        var valor = await _contexto.ConfiguracionesSmtp.AsNoTracking().Where(x => x.Activo).Select(x => x.PasswordCifrada).SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(valor)) return null;
        try { return _protector.Unprotect(valor); }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException || ex is FormatException)
        { throw new InvalidOperationException("No se pudo descifrar la contraseña SMTP.", ex); }
    }

    public async Task EnviarPruebaAsync(CancellationToken cancellationToken = default)
    {
        var configuracion = await ObtenerAsync(cancellationToken);
        var entidad = configuracion.Entidad
            ?? throw new InvalidOperationException("No existe una configuración SMTP activa.");
        var password = await ObtenerPasswordAsync(cancellationToken)
            ?? throw new InvalidOperationException("Guarde la contraseña SMTP antes de enviar la prueba.");
        if (string.IsNullOrWhiteSpace(entidad.EmailRemitente))
            throw new InvalidOperationException("El correo remitente es requerido.");
        if (entidad.UsarSsl && entidad.UsarTls)
            throw new InvalidOperationException("Seleccione SSL directo o STARTTLS, no ambos.");

        var socket = entidad.UsarSsl
            ? SecureSocketOptions.SslOnConnect
            : entidad.UsarTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        using var cliente = new SmtpClient();
        await cliente.ConnectAsync(entidad.Servidor, entidad.Puerto, socket, cancellationToken);
        await cliente.AuthenticateAsync(entidad.Usuario, password, cancellationToken);

        var mensaje = new MimeKit.MimeMessage();
        mensaje.From.Add(new MimeKit.MailboxAddress(entidad.NombreRemitente ?? entidad.EmailRemitente, entidad.EmailRemitente));
        mensaje.To.Add(MimeKit.MailboxAddress.Parse(entidad.EmailRemitente));
        mensaje.Subject = "Prueba SMTP - SRAUMOAR";
        mensaje.Body = new MimeKit.BodyBuilder
        {
            HtmlBody = $"<h2>Prueba SMTP correcta</h2><p>El sistema SRAUMOAR pudo autenticarse y enviar este correo usando <strong>{entidad.Servidor}:{entidad.Puerto}</strong>.</p><p>Modo: {(entidad.UsarSsl ? "SSL directo" : entidad.UsarTls ? "STARTTLS" : "Automático")}.</p>"
        }.ToMessageBody();
        await cliente.SendAsync(mensaje, cancellationToken);
        await cliente.DisconnectAsync(true, cancellationToken);
    }

    public async Task ProbarAsync(CancellationToken cancellationToken = default)
    {
        var configuracion = await ObtenerAsync(cancellationToken);
        var entidad = configuracion.Entidad
            ?? throw new InvalidOperationException("No existe una configuración SMTP activa.");
        var password = await ObtenerPasswordAsync(cancellationToken)
            ?? throw new InvalidOperationException("Guarde la contraseña SMTP antes de probar la conexión.");
        if (string.IsNullOrWhiteSpace(entidad.Servidor))
            throw new InvalidOperationException("El servidor SMTP es requerido.");
        if (string.IsNullOrWhiteSpace(entidad.Usuario))
            throw new InvalidOperationException("El usuario SMTP es requerido.");
        if (entidad.UsarSsl && entidad.UsarTls)
            throw new InvalidOperationException("Seleccione SSL directo o STARTTLS, no ambos.");

        var socket = entidad.UsarSsl
            ? SecureSocketOptions.SslOnConnect
            : entidad.UsarTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        using var cliente = new SmtpClient();
        await cliente.ConnectAsync(entidad.Servidor, entidad.Puerto, socket, cancellationToken);
        await cliente.AuthenticateAsync(entidad.Usuario, password, cancellationToken);
        await cliente.DisconnectAsync(true, cancellationToken);
    }
}
