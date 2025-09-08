using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Docentes;

namespace SRAUMOAR.Pages.portal.docente
{
    //solo rol Docentes
    [Authorize(Roles = "Docentes")]
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;

        public IndexModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        [Display(Name = "Contraseña actual")]
        [Required(ErrorMessage = "La contraseña actual es requerida")]
        public string ContrasenaActual { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Nueva contraseña")]
        [MinLength(6, ErrorMessage = "La nueva contraseña debe de ser de al menos 6 caracteres")]
        [Required(ErrorMessage = "La nueva contraseña es requerida")]
        public string NuevaContrasena { get; set; } = string.Empty;

        [BindProperty]
        [Display(Name = "Confirmar nueva contraseña")]
        [Compare("NuevaContrasena", ErrorMessage = "La confirmación no coincide con la nueva contraseña")]
        [Required(ErrorMessage = "Confirma la nueva contraseña")]
        public string ConfirmarContrasena { get; set; } = string.Empty;

        public void OnGet() { }

        public async Task<IActionResult> OnPostCambiarContrasenaAsync()
        {
            if (!ModelState.IsValid)
            {
                TempData["PasswordErrorMessage"] = "Revisa los datos del formulario.";
                return RedirectToPage();
            }

            try
            {
                var userIdClaim = User.FindFirstValue("UserId") ?? "0";
                if (!int.TryParse(userIdClaim, out var usuarioActualId))
                {
                    TempData["PasswordErrorMessage"] = "No se pudo identificar al usuario actual.";
                    return RedirectToPage();
                }

                var docente = await _context.Docentes.FirstOrDefaultAsync(d => d.UsuarioId == usuarioActualId);
                if (docente == null)
                {
                    TempData["PasswordErrorMessage"] = "No se encontró el docente asociado al usuario actual.";
                    return RedirectToPage();
                }

                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == usuarioActualId);
                if (usuario == null)
                {
                    TempData["PasswordErrorMessage"] = "No se encontró el usuario.";
                    return RedirectToPage();
                }

                if (!string.Equals(usuario.Clave, ContrasenaActual))
                {
                    TempData["PasswordErrorMessage"] = "La contraseña actual no es correcta. Si no la recuerdas, contacta al administrador.";
                    return RedirectToPage();
                }

                usuario.Clave = NuevaContrasena;
                _context.Update(usuario);
                await _context.SaveChangesAsync();

                TempData["PasswordSuccessMessage"] = "La contraseña se actualizó correctamente.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["PasswordErrorMessage"] = "Error al actualizar la contraseña: " + ex.Message;
                return RedirectToPage();
            }
        }
    }
}
