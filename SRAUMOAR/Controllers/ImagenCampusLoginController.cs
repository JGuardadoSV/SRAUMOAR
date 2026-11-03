using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SRAUMOAR.Controllers;

public sealed class ImagenCampusLoginController(IWebHostEnvironment entorno) : Controller
{
    internal const string Archivo = "campus-login.jpg";

    [AllowAnonymous]
    [HttpGet("imagen-campus-login", Name = "ImagenCampusLogin")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Mostrar()
    {
        var personalizada = Path.Combine(entorno.ContentRootPath, "storage", Archivo);
        return System.IO.File.Exists(personalizada)
            ? PhysicalFile(personalizada, "image/jpeg")
            : File("~/images/campus-login.png", "image/png");
    }
}
