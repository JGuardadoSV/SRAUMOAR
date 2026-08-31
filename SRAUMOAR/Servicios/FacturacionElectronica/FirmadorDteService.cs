using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
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
    public async Task<string> FirmarAsync(string rutaCertificado, string password, string json, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(rutaCertificado))
            throw new FileNotFoundException("No se encontró el certificado .crt.", rutaCertificado);
        if (string.IsNullOrWhiteSpace(password))
            throw new UnauthorizedAccessException("La contraseña del certificado está vacía.");

        var certificado = await ObtenerCertificadoAsync(rutaCertificado, cancellationToken);
        ValidarNitDocumento(certificado, json);
        var hashGuardado = certificado.privateKey?.clave ?? certificado.clavePri;
        if (!VerificarPassword(password, hashGuardado))
            throw new UnauthorizedAccessException("La contraseña del certificado no es válida.");
        if (certificado.privateKey?.encodied is not { Length: > 0 } clavePrivada)
            throw new InvalidOperationException("El certificado no contiene la clave privada esperada.");

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(clavePrivada, out var leidos);
        if (leidos != clavePrivada.Length) throw new InvalidOperationException("La clave privada contiene datos sobrantes.");
        return await Task.Run(() => JWT.Encode(json, rsa, JwsAlgorithm.RS512), cancellationToken);
    }

    public void LimpiarCache(string? rutaCertificado = null)
    {
        // No se retienen llaves por ruta: una sustitución atómica debe verse en la siguiente firma.
    }

    private async Task<CertificadoMh> ObtenerCertificadoAsync(string ruta, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, true);
        return await Task.Run(() => LeerCertificadoValidado(stream), cancellationToken);
    }

    internal static CertificadoMh LeerCertificadoValidado(Stream contenido, string? nitEsperado = null)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 10 * 1024 * 1024,
            CloseInput = false
        };
        using var reader = XmlReader.Create(contenido, settings);
        var xml = XDocument.Load(reader);
        var root = xml.Root;
        if (root?.Name != XName.Get("CertificadoMH") || root.Elements("nit").Count() != 1 ||
            root.Elements("privateKey").Count() != 1 ||
            root.Element("privateKey")!.Elements("encodied").Count() != 1 ||
            root.Element("privateKey")!.Elements("clave").Count() > 1 || root.Elements("clavePri").Count() > 1)
            throw new InvalidOperationException("El certificado no contiene la estructura XML/privateKey esperada.");
        // XNodeReader no implementa ReadElementContentAsBase64, requerido por XmlSerializer para byte[].
        using var texto = new StringReader(xml.ToString());
        using var documentReader = XmlReader.Create(texto, settings);
        var certificado = (CertificadoMh?)new XmlSerializer(typeof(CertificadoMh)).Deserialize(documentReader)
            ?? throw new InvalidOperationException("El certificado .crt no tiene un formato válido.");
        var nit = NormalizarNitCertificado(certificado.nit);
        if (nitEsperado is not null && nit != NormalizarNitCertificado(nitEsperado))
            throw new InvalidOperationException("El NIT del certificado no coincide con el emisor.");
        if (certificado.privateKey?.encodied is not { Length: > 0 } privada)
            throw new InvalidOperationException("El certificado no contiene una clave privada.");
        var hash = (certificado.privateKey.clave ?? certificado.clavePri)?.Trim();
        if (hash is null || hash.Length != 128 || !hash.All(Uri.IsHexDigit))
            throw new InvalidOperationException("El certificado no contiene un hash SHA-512 válido de la contraseña privada.");
        if ((!string.IsNullOrWhiteSpace(certificado.privateKey.algorithm) &&
             !string.Equals(certificado.privateKey.algorithm, "RSA", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(certificado.privateKey.format) &&
             !string.Equals(certificado.privateKey.format, "PKCS#8", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("La clave privada debe ser RSA PKCS#8.");
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privada, out var leidos);
        if (leidos != privada.Length) throw new InvalidOperationException("La clave privada contiene datos sobrantes.");
        return certificado;
    }

    internal static string NormalizarNitCertificado(string? nit)
    {
        var normalizado = (nit ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        if ((normalizado.Length != 9 && normalizado.Length != 14) || !normalizado.All(c => c is >= '0' and <= '9'))
            throw new InvalidOperationException("El NIT del certificado debe contener 9 o 14 dígitos.");
        return normalizado;
    }

    internal static void ValidarNitDocumento(CertificadoMh certificado, string json)
    {
        using var documento = JsonDocument.Parse(json);
        var root = documento.RootElement;
        string? nit = null;
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("emisor", out var emisor) && emisor.ValueKind == JsonValueKind.Object)
            {
                if (emisor.TryGetProperty("nit", out var valor) && valor.ValueKind == JsonValueKind.String) nit = valor.GetString();
                if (string.IsNullOrWhiteSpace(nit) && emisor.TryGetProperty("numDocumento", out valor) && valor.ValueKind == JsonValueKind.String)
                    nit = valor.GetString();
            }
            else if (root.TryGetProperty("donatario", out var donatario) && donatario.ValueKind == JsonValueKind.Object &&
                donatario.TryGetProperty("numDocumento", out var documentoDonatario) && documentoDonatario.ValueKind == JsonValueKind.String)
                nit = documentoDonatario.GetString();
        }
        if (NormalizarNitCertificado(nit) != NormalizarNitCertificado(certificado.nit))
            throw new InvalidOperationException("El NIT del certificado no coincide con el emisor/donatario del JSON.");
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
