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
        if (contenido.CanSeek && contenido.Length > MaxBytes)
            throw new InvalidOperationException("El certificado supera el límite permitido de 10 MB.");

        var nitSeguro = FirmadorDteService.NormalizarNitCertificado(nit);

        var directorio = Path.Combine(_environment.ContentRootPath, "storage", "certificados", ambiente);
        Directory.CreateDirectory(directorio);
        var ruta = Path.Combine(directorio, $"{nitSeguro}.crt");
        // Staging en el mismo volumen: ni XML inválido ni cancelaciones truncan el certificado anterior.
        var temporal = Path.Combine(directorio, $".{nitSeguro}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var archivo = new FileStream(temporal, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int leidos;
                while ((leidos = await contenido.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
                {
                    total += leidos;
                    if (total > MaxBytes) throw new InvalidOperationException("El certificado supera el límite permitido de 10 MB.");
                    await archivo.WriteAsync(buffer.AsMemory(0, leidos), cancellationToken);
                }
                await archivo.FlushAsync(cancellationToken);
                archivo.Position = 0;
                FirmadorDteService.LeerCertificadoValidado(archivo, nitSeguro);
                archivo.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(ruta)) File.Replace(temporal, ruta, null);
            else File.Move(temporal, ruta); // Si apareció otro archivo, no sobrescribirlo sin Replace.
            return ruta;
        }
        finally
        {
            if (File.Exists(temporal)) File.Delete(temporal);
        }
    }

    public string ObtenerRuta(string ambienteFiscal, string nit)
    {
        var ambiente = ConfiguracionFeService.NormalizarAmbiente(ambienteFiscal);
        var nitSeguro = FirmadorDteService.NormalizarNitCertificado(nit);
        return Path.Combine(_environment.ContentRootPath, "storage", "certificados", ambiente, $"{nitSeguro}.crt");
    }
}
