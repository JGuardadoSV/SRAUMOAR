using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Entidades.Accesos;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;

namespace SRAUMOAR.Pages
{
    [AllowAnonymous]
    public class IndexModel : PageModel
    {
        private readonly EmisorConfig _emisor;



        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel( SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
            
        }
        [BindProperty]
        public LoginModel? LoginData { get; set; }
        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var usuarios = await _context.Usuarios
                    .Where(x => x.NombreUsuario == LoginData.NombreUsuario && x.Clave == LoginData.Clave && x.Activo == true)
                    .Include(x => x.NivelAcceso)
                    .OrderBy(x => x.IdUsuario)
                    .ToListAsync();

                if (usuarios.Any())
                {
                    var usuario = usuarios.First();
                    string nombre;
                    string idalumno = "0";

                    if (string.Equals(usuario.NivelAcceso?.Nombre, "Estudiantes", StringComparison.OrdinalIgnoreCase))
                    {
                        var usuarioIds = usuarios
                            .Where(x => string.Equals(x.NivelAcceso?.Nombre, "Estudiantes", StringComparison.OrdinalIgnoreCase))
                            .Select(x => x.IdUsuario)
                            .ToList();

                        var alumnosPorUsuario = await _context.Alumno
                            .Where(x => x.UsuarioId.HasValue && usuarioIds.Contains(x.UsuarioId.Value))
                            .ToDictionaryAsync(x => x.UsuarioId!.Value);

                        var usuarioValido = usuarios
                            .Where(x => string.Equals(x.NivelAcceso?.Nombre, "Estudiantes", StringComparison.OrdinalIgnoreCase))
                            .FirstOrDefault(x => alumnosPorUsuario.ContainsKey(x.IdUsuario));

                        if (usuarioValido == null)
                        {
                            ModelState.AddModelError(string.Empty, "Se encontraron usuarios estudiante con estas credenciales, pero ninguno está vinculado a un registro de alumno. Contacta al administrador.");
                            return Page();
                        }

                        usuario = usuarioValido;
                        var alumno = alumnosPorUsuario[usuario.IdUsuario];
                        nombre = alumno.Nombres ?? usuario.NombreUsuario;
                        idalumno = alumno.AlumnoId.ToString();
                    }
                    else
                    {
                        try
                        {
                            nombre = await _context.Docentes
                                .Where(x => x.UsuarioId == usuario.IdUsuario)
                                .Select(x => x.Nombres)
                                .FirstOrDefaultAsync() ?? usuario.NombreUsuario;
                        }
                        catch (Exception)
                        {
                            nombre = usuario.NombreUsuario;
                        }
                    }
                    

                    var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, value: usuario.NombreUsuario),
                    new Claim(ClaimTypes.Role, usuario.NivelAcceso!.Nombre.ToString()),
                    new Claim(ClaimTypes.Email, usuario.NombreUsuario),
                    new Claim("NombreCompleto", nombre),
                     new Claim("idalumno", idalumno),
                    new Claim("UserId", usuario.IdUsuario.ToString())
                };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                    switch (usuario.NivelAcceso!.Nombre.ToLower())
                    {
                        case "administrador":
                            return RedirectToPage("/Home");
                        case "administracion":
                            return RedirectToPage("/Menu");
                        case "contabilidad":
                            return RedirectToPage("/Menu");
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



