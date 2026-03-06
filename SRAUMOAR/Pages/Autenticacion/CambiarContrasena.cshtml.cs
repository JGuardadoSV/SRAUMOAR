using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.Autenticacion
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class CambiarContrasenaModel : PageModel
    {
        private readonly Contexto _context;

        public CambiarContrasenaModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        [Display(Name = "Contrasena actual")]
        [Required(ErrorMessage = "La contrasena actual es requerida")]
        public string ContrasenaActual { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Nueva contrasena")]
        [MinLength(6, ErrorMessage = "La nueva contrasena debe tener al menos 6 caracteres")]
        [Required(ErrorMessage = "La nueva contrasena es requerida")]
        public string NuevaContrasena { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Confirmar nueva contrasena")]
        [Compare("NuevaContrasena", ErrorMessage = "La confirmacion no coincide con la nueva contrasena")]
        [Required(ErrorMessage = "La confirmacion es requerida")]
        public string ConfirmarContrasena { get; set; } = string.Empty;

        public string NombreUsuarioActual { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            await CargarUsuarioActualAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await CargarUsuarioActualAsync();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userIdClaim = User.FindFirstValue("UserId");
            if (!int.TryParse(userIdClaim, out var usuarioActualId))
            {
                ModelState.AddModelError(string.Empty, "No se pudo identificar al usuario actual.");
                return Page();
            }

            var usuario = await _context.Usuarios
                .Include(u => u.NivelAcceso)
                .FirstOrDefaultAsync(u => u.IdUsuario == usuarioActualId && u.Activo == true);

            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "No se encontro el usuario actual.");
                return Page();
            }

            if (!string.Equals(usuario.Clave, ContrasenaActual))
            {
                ModelState.AddModelError(nameof(ContrasenaActual), "La contrasena actual no es correcta.");
                return Page();
            }

            usuario.Clave = NuevaContrasena;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "La contrasena se actualizo correctamente.";
            return RedirectToPage();
        }

        private async Task CargarUsuarioActualAsync()
        {
            var userIdClaim = User.FindFirstValue("UserId");
            if (!int.TryParse(userIdClaim, out var usuarioActualId))
            {
                NombreUsuarioActual = User.Identity?.Name ?? string.Empty;
                return;
            }

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdUsuario == usuarioActualId);

            NombreUsuarioActual = usuario?.NombreUsuario ?? (User.Identity?.Name ?? string.Empty);
        }
    }
}
