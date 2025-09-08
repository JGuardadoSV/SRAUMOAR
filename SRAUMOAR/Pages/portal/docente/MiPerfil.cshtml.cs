using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Modelos;
using System.Security.Claims;

namespace SRAUMOAR.Pages.portal.docente
{
    [Authorize(Roles = "Docentes")]
    public class MiPerfilModel : PageModel
    {
        private readonly Contexto _context;

        public MiPerfilModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Docente Docente { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirstValue("UserId") ?? "0";
            if (!int.TryParse(userIdClaim, out var usuarioActualId))
            {
                return Unauthorized();
            }

            var docente = await _context.Docentes.FirstOrDefaultAsync(d => d.UsuarioId == usuarioActualId);
            if (docente == null)
            {
                return NotFound();
            }

            Docente = docente;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Remover validaciones de propiedades requeridas que no se editan en el formulario
            ModelState.Remove("Docente.Nombres");
            ModelState.Remove("Docente.Apellidos");
            ModelState.Remove("Docente.Dui");
            ModelState.Remove("Docente.FechaDeNacimiento");
            ModelState.Remove("Docente.ProfesionId");
            ModelState.Remove("Docente.Genero");
            ModelState.Remove("Docente.FechaDeIngreso");

            var userIdClaim = User.FindFirstValue("UserId") ?? "0";
            if (!int.TryParse(userIdClaim, out var usuarioActualId))
            {
                TempData["ErrorMessage"] = "No se pudo identificar al usuario actual.";
                return RedirectToPage();
            }

            if (!ModelState.IsValid)
            {
                // Recargar datos desde BD para no perder campos no editables
                var docenteActual = await _context.Docentes.FirstOrDefaultAsync(d => d.UsuarioId == usuarioActualId);
                if (docenteActual != null)
                {
                    Docente = docenteActual;
                }
                return Page();
            }

            try
            {
                var docenteExistente = await _context.Docentes.FirstOrDefaultAsync(d => d.DocenteId == Docente.DocenteId && d.UsuarioId == usuarioActualId);
                if (docenteExistente == null)
                {
                    TempData["ErrorMessage"] = "No se encontró el docente.";
                    return RedirectToPage();
                }

                // Actualizar campos editables
                docenteExistente.Telefono = Docente.Telefono;
                docenteExistente.Direccion = Docente.Direccion;
                docenteExistente.Email = Docente.Email;

                // Sincronizar con Usuario
                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == usuarioActualId);
                if (usuario != null)
                {
                    usuario.Email = Docente.Email;
                    usuario.NombreUsuario = Docente.Email; // Acordado: sincronizar nombre de usuario con email
                    _context.Update(usuario);
                }

                _context.Update(docenteExistente);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Los datos se han actualizado correctamente.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al guardar los cambios: " + ex.Message;
                return RedirectToPage();
            }
        }
    }
}


