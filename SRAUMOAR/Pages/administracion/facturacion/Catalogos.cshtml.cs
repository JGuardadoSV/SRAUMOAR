using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.administracion.facturacion;

[Authorize(Roles = "Administrador,Administracion")]
public sealed class CatalogosModel : PageModel
{
    private readonly ICatalogoFeService _catalogos;

    public CatalogosModel(ICatalogoFeService catalogos) => _catalogos = catalogos;

    [BindProperty]
    public IFormFile? Archivo { get; set; }

    [BindProperty]
    public string Version { get; set; } = "1.1";

    public int? FilasImportadas { get; private set; }
    public string? Mensaje { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnPostImportarAsync(CancellationToken cancellationToken)
    {
        if (Archivo is null || Archivo.Length == 0)
        {
            Error = "Seleccione el Excel de catálogos.";
            return Page();
        }
        if (!string.Equals(Path.GetExtension(Archivo.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            Error = "El catálogo debe estar en formato .xlsx.";
            return Page();
        }
        if (Archivo.Length > 10 * 1024 * 1024)
        {
            Error = "El archivo supera el máximo de 10 MB.";
            return Page();
        }

        try
        {
            await using var stream = Archivo.OpenReadStream();
            FilasImportadas = await _catalogos.ImportarAsync(stream, Version, cancellationToken);
            Mensaje = $"Se importaron {FilasImportadas} filas de catálogos, versión {Version}.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        return Page();
    }
}
