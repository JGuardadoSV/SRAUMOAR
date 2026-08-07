namespace SRAUMOAR.Servicios.FacturacionElectronica;

public interface ICertificadoStorageService
{
    Task<string> GuardarAsync(string ambienteFiscal, string nit, Stream contenido, string nombreArchivo, CancellationToken cancellationToken = default);
    string ObtenerRuta(string ambienteFiscal, string nit);
}

public sealed class CertificadoStorageService : ICertificadoStorageService
{
    private readonly IWebHostEnvironment _environment;
    private const long MaxBytes = 10 * 1024 * 1024;

    public CertificadoStorageService(IWebHostEnvironment environment) => _environment = environment;

    public async Task<string> GuardarAsync(string ambienteFiscal, string nit, Stream contenido, string nombreArchivo, CancellationToken cancellationToken = default)
    {
        var ambiente = ConfiguracionFeService.NormalizarAmbiente(ambienteFiscal);
        if (!string.Equals(Path.GetExtension(nombreArchivo), ".crt", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El certificado debe tener extensión .crt.");
        if (contenido.Length > MaxBytes)
            throw new InvalidOperationException("El certificado supera el límite permitido de 10 MB.");

        var nitSeguro = new string((nit ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(nitSeguro))
            throw new InvalidOperationException("El NIT es requerido para almacenar el certificado.");

        var directorio = Path.Combine(_environment.ContentRootPath, "storage", "certificados", ambiente);
        Directory.CreateDirectory(directorio);
        var ruta = Path.Combine(directorio, $"{nitSeguro}.crt");
        await using var archivo = new FileStream(ruta, FileMode.Create, FileAccess.Write, FileShare.None);
        await contenido.CopyToAsync(archivo, cancellationToken);
        return ruta;
    }

    public string ObtenerRuta(string ambienteFiscal, string nit)
    {
        var ambiente = ConfiguracionFeService.NormalizarAmbiente(ambienteFiscal);
        var nitSeguro = new string((nit ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        return Path.Combine(_environment.ContentRootPath, "storage", "certificados", ambiente, $"{nitSeguro}.crt");
    }
}
