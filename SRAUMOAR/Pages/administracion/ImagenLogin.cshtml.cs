using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SixLabors.ImageSharp;
using SRAUMOAR.Controllers;

namespace SRAUMOAR.Pages.administracion;

[Authorize(Roles = "Administrador,Administracion")]
[RequestSizeLimit(6 * 1024 * 1024)]
public sealed class ImagenLoginModel(IWebHostEnvironment entorno) : PageModel
{
    public async Task<IActionResult> OnPostAsync(IFormFile? imagen, CancellationToken ct)
    {
        if (imagen is null || imagen.Length is <= 0 or > 5 * 1024 * 1024)
        {
            ModelState.AddModelError("", "Selecciona una imagen JPEG o PNG de hasta 5 MB.");
            return Page();
        }

        await using var entrada = imagen.OpenReadStream();
        try
        {
            var formato = await Image.DetectFormatAsync(entrada, ct);
            entrada.Position = 0;
            var info = await Image.IdentifyAsync(entrada, ct);
            if (formato.Name is not ("JPEG" or "PNG") || (long)info.Width * info.Height > 16_000_000)
            {
                ModelState.AddModelError("", "Usa una imagen JPEG o PNG de hasta 16 megapíxeles.");
                return Page();
            }

            entrada.Position = 0;
            using var foto = await Image.LoadAsync(new SixLabors.ImageSharp.Formats.DecoderOptions { MaxFrames = 1 }, entrada, ct);
            var carpeta = Path.Combine(entorno.ContentRootPath, "storage");
            Directory.CreateDirectory(carpeta);
            var temporal = Path.Combine(carpeta, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await foto.SaveAsJpegAsync(temporal, ct);
                System.IO.File.Move(temporal, Path.Combine(carpeta, ImagenCampusLoginController.Archivo), overwrite: true);
            }
            finally { System.IO.File.Delete(temporal); }
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            ModelState.AddModelError("", "El archivo no se pudo leer como imagen.");
            return Page();
        }

        TempData["ImagenLoginGuardada"] = "Imagen de inicio de sesión actualizada.";
        return RedirectToPage();
    }
}
