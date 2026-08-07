using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
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
}
