using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using Jose;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

[XmlRoot("CertificadoMH")]
public sealed class CertificadoMh
{
    public string? nit { get; set; }
    public LlaveCertificado? publicKey { get; set; }
    public LlaveCertificado? privateKey { get; set; }
    public string? clavePri { get; set; }
}

public sealed class LlaveCertificado
{
    public string? keyType { get; set; }
    public string? algorithm { get; set; }
    public byte[]? encodied { get; set; }
    public string? format { get; set; }
    public string? clave { get; set; }
}

public interface IFirmadorDteService
{
    Task<string> FirmarAsync(string rutaCertificado, string password, string json, CancellationToken cancellationToken = default);
    void LimpiarCache(string? rutaCertificado = null);
}

public sealed class FirmadorDteService : IFirmadorDteService
{
    private readonly ConcurrentDictionary<string, CertificadoMh> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> FirmarAsync(string rutaCertificado, string password, string json, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(rutaCertificado))
            throw new FileNotFoundException("No se encontró el certificado .crt.", rutaCertificado);
        if (string.IsNullOrWhiteSpace(password))
            throw new UnauthorizedAccessException("La contraseña del certificado está vacía.");

        var certificado = await ObtenerCertificadoAsync(rutaCertificado, cancellationToken);
        var hashGuardado = certificado.privateKey?.clave ?? certificado.clavePri;
        if (!VerificarPassword(password, hashGuardado))
            throw new UnauthorizedAccessException("La contraseña del certificado no es válida.");
        if (certificado.privateKey?.encodied is not { Length: > 0 } clavePrivada)
            throw new InvalidOperationException("El certificado no contiene la clave privada esperada.");

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(clavePrivada, out _);
        return await Task.Run(() => JWT.Encode(json, rsa, JwsAlgorithm.RS512), cancellationToken);
    }

    public void LimpiarCache(string? rutaCertificado = null)
    {
        if (string.IsNullOrWhiteSpace(rutaCertificado)) _cache.Clear();
        else _cache.TryRemove(rutaCertificado, out _);
    }

    private async Task<CertificadoMh> ObtenerCertificadoAsync(string ruta, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(ruta, out var existente)) return existente;
        var serializer = new XmlSerializer(typeof(CertificadoMh));
        await using var stream = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        var certificado = await Task.Run(() => serializer.Deserialize(stream) as CertificadoMh, cancellationToken)
            ?? throw new InvalidOperationException("El certificado .crt no tiene un formato válido.");
        _cache.TryAdd(ruta, certificado);
        return certificado;
    }

    private static bool VerificarPassword(string password, string? hashGuardado)
    {
        if (string.IsNullOrWhiteSpace(hashGuardado)) return false;
        using var sha = SHA512.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash.ToUpperInvariant()),
            Encoding.UTF8.GetBytes(hashGuardado.Trim().ToUpperInvariant()));
    }
}
