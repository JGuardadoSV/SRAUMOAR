using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Accesos;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SRAUMOAR.Pages.portal.estudiante
{
    public class MiPerfilModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public MiPerfilModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Alumno Alumno { get; set; } = default!;

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

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var alumno =  await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == id);
            if (alumno == null)
            {
                return NotFound();
            }
            Alumno = alumno;
           ViewData["CarreraId"] = new SelectList(_context.Carreras, "CarreraId", "CodigoCarrera");
           ViewData["MunicipioId"] = new SelectList(_context.Municipios, "MunicipioId", "NombreMunicipio");
           ViewData["UsuarioId"] = new SelectList(_context.Usuarios, "IdUsuario", "Clave");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // Removemos la validación de campos que no estamos editando
            ModelState.Remove("Alumno.Nombres");
            ModelState.Remove("Alumno.Apellidos");
            ModelState.Remove("Alumno.FechaDeNacimiento");
            ModelState.Remove("Alumno.Email");
            ModelState.Remove("Alumno.ContactoDeEmergencia");
            ModelState.Remove("Alumno.NumeroDeEmergencia");
            ModelState.Remove("Alumno.DireccionDeResidencia");
            ModelState.Remove("Alumno.Estado");
            ModelState.Remove("Alumno.IngresoPorEquivalencias");
            ModelState.Remove("Alumno.Fotografia");
            ModelState.Remove("Alumno.Foto");
            ModelState.Remove("Alumno.Carnet");
            ModelState.Remove("Alumno.Genero");
            ModelState.Remove("Alumno.UsuarioId");
            ModelState.Remove("Alumno.MunicipioId");
            ModelState.Remove("Alumno.CarreraId");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var alumnoExistente = await _context.Alumno.FindAsync(Alumno.AlumnoId);
                if (alumnoExistente == null)
                {
                    return NotFound();
                }

                // Solo actualizamos los campos específicos
                alumnoExistente.DUI = Alumno.DUI;
                alumnoExistente.TelefonoPrimario = Alumno.TelefonoPrimario;
                alumnoExistente.Whatsapp = Alumno.Whatsapp;
                alumnoExistente.TelefonoSecundario = Alumno.TelefonoSecundario;

                _context.Update(alumnoExistente);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Los datos se han actualizado correctamente.";
                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar los cambios: " + ex.Message);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCambiarContrasenaAsync()
        {
            // Validaciones de modelo para las contraseñas
            if (!ModelState.IsValid)
            {
                TempData["PasswordErrorMessage"] = "Revisa los datos del formulario.";
                return RedirectToPage();
            }

            try
            {
                // Identificar usuario autenticado y alumno
                var userIdClaim = User.FindFirstValue("UserId") ?? "0";
                if (!int.TryParse(userIdClaim, out var usuarioActualId))
                {
                    TempData["PasswordErrorMessage"] = "No se pudo identificar al usuario actual.";
                    return RedirectToPage();
                }

                // Buscar alumno asociado al usuario autenticado
                var alumno = await _context.Alumno.FirstOrDefaultAsync(a => a.UsuarioId == usuarioActualId);
                if (alumno == null)
                {
                    TempData["PasswordErrorMessage"] = "No se encontró el alumno asociado al usuario actual.";
                    return RedirectToPage();
                }

                // Buscar usuario
                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == usuarioActualId);
                if (usuario == null)
                {
                    TempData["PasswordErrorMessage"] = "No se encontró el usuario.";
                    return RedirectToPage();
                }

                // Validar contraseña actual
                if (!string.Equals(usuario.Clave, ContrasenaActual))
                {
                    TempData["PasswordErrorMessage"] = "La contraseña actual no es correcta. Si no la recuerdas, contacta al administrador.";
                    return RedirectToPage();
                }

                // Actualizar contraseña
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

        private bool AlumnoExists(int id)
        {
            return _context.Alumno.Any(e => e.AlumnoId == id);
        }
    }
}
