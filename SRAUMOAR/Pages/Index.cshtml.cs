using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Entidades.Accesos;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace SRAUMOAR.Pages
{
    public class IndexModel : PageModel
    {
        
        

        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel( SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
            
        }
        [BindProperty]
        public LoginModel? LoginData { get; set; }
        public async Task<IActionResult> OnPostAsync()
        {
            bool existealumno = false;
            if (ModelState.IsValid)
            {
                var usuario = await _context.Usuarios.Where(x=>x.NombreUsuario==LoginData.NombreUsuario && x.Clave==LoginData.Clave && x.Activo==true).Include(x=>x.NivelAcceso).FirstOrDefaultAsync();
                if (usuario != null)
                {
                    string nombre;
                    try
                    {
                        if (usuario.NivelAcceso.Nombre == "Estudiantes")
                        {
                            nombre = _context.Alumno.Where(x => x.UsuarioId == usuario.IdUsuario).First().Nombres;
                            var alumno = _context.Alumno.Where(x => x.UsuarioId == usuario.IdUsuario).FirstOrDefault();
                            if (alumno != null)
                            {
                                existealumno = true;
                            }
                            else
                            {
                                existealumno = false;
                                ModelState.AddModelError(string.Empty, "Nombre de usuario o contraseña incorrectos.");
                            }
                        }
                        else
                        {
                            nombre = _context.Docentes.Where(x => x.UsuarioId == usuario.IdUsuario).First().Nombres;
                        }
                    }
                    catch (Exception)
                    {

                        nombre = "NOMBRE_USUARIO";
                    }
                    

                    var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, value: usuario.NombreUsuario),
                    new Claim(ClaimTypes.Role, usuario.NivelAcceso.Nombre.ToString()),
                    new Claim(ClaimTypes.Email, usuario.NombreUsuario),
                    new Claim("NombreCompleto", nombre),
                    new Claim("UserId", usuario.IdUsuario.ToString())
                };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                    switch (usuario.NivelAcceso.Nombre.ToLower())
                    {
                        case "administrador":
                            return RedirectToPage("/Home");
                        case "administracion":
                            return RedirectToPage("/Home");
                        case "docentes":
                            return RedirectToPage("/portal/docente/Index");
                        case "estudiantes":
                            return RedirectToPage("/portal/estudiante/Index");
                        default:
                            return new StatusCodeResult(StatusCodes.Status403Forbidden);
                    }
                }
                ModelState.AddModelError(string.Empty, "Nombre de usuario o contraseña incorrectos.");
            }
            return Page();
        }
    }
}


/*
 public IActionResult AlgunaAccion()
{
    var userName = User.Identity.Name;
    var userEmail = User.FindFirstValue(ClaimTypes.Email);
    var userRole = User.FindFirstValue(ClaimTypes.Role);
    var fullName = User.FindFirstValue("FullName");
    var userId = User.FindFirstValue("UserId");

    // Usa estos datos como necesites
}

// En una página Razor
@page
@model MiPagina
@{
    var userName = User.Identity.Name;
    var userEmail = User.FindFirstValue(ClaimTypes.Email);
    var userRole = User.FindFirstValue(ClaimTypes.Role);
    var fullName = User.FindFirstValue("FullName");
    var userId = User.FindFirstValue("UserId");
}
 
 */