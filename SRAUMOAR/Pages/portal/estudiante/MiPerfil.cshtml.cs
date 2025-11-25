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

        public List<string> DocumentosPendientes { get; set; } = new List<string>();

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

            // Calcular documentos pendientes
            DocumentosPendientes = ObtenerDocumentosPendientes(alumno);

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
            try
            {
                // 1) Resolver alumno por AlumnoId posteado (fuente de verdad en este formulario)
                Alumno? alumno = null;
                if (Alumno != null && Alumno.AlumnoId != 0)
                {
                    alumno = await _context.Alumno.FirstOrDefaultAsync(a => a.AlumnoId == Alumno.AlumnoId);
                }
                // Respaldo: si no vino AlumnoId, intentar resolver por claim
                if (alumno == null)
                {
                    var userIdClaim = User.FindFirstValue("UserId") ?? "0";
                    if (int.TryParse(userIdClaim, out var usuarioActualIdTmp))
                    {
                        alumno = await _context.Alumno.FirstOrDefaultAsync(a => a.UsuarioId == usuarioActualIdTmp);
                    }
                }
                if (alumno == null)
                {
                    TempData["PasswordErrorMessage"] = "No se pudo cargar el alumno para actualizar la contraseña.";
                    return RedirectToPage();
                }

                // Validación manual de contraseñas
                if (string.IsNullOrWhiteSpace(ContrasenaActual)
                    || string.IsNullOrWhiteSpace(NuevaContrasena)
                    || string.IsNullOrWhiteSpace(ConfirmarContrasena))
                {
                    TempData["PasswordErrorMessage"] = "Completa todos los campos de contraseña.";
                    return RedirectToPage(new { id = alumno.AlumnoId });
                }
                if (NuevaContrasena.Length < 6)
                {
                    TempData["PasswordErrorMessage"] = "La nueva contraseña debe tener al menos 6 caracteres.";
                    return RedirectToPage(new { id = alumno.AlumnoId });
                }
                if (!string.Equals(NuevaContrasena, ConfirmarContrasena))
                {
                    TempData["PasswordErrorMessage"] = "La confirmación no coincide con la nueva contraseña.";
                    return RedirectToPage(new { id = alumno.AlumnoId });
                }

                // 2) Buscar usuario por el UsuarioId asociado al alumno
                if (alumno.UsuarioId == null)
                {
                    TempData["PasswordErrorMessage"] = "El alumno no tiene un usuario asociado.";
                    return RedirectToPage(new { id = alumno.AlumnoId });
                }
                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == alumno.UsuarioId.Value);
                if (usuario == null)
                {
                    TempData["PasswordErrorMessage"] = "No se encontró el usuario.";
                    return RedirectToPage(new { id = alumno.AlumnoId });
                }

                // Validar contraseña actual
                if (!string.Equals(usuario.Clave, ContrasenaActual))
                {
                    TempData["PasswordErrorMessage"] = "La contraseña actual no es correcta. Si no la recuerdas, contacta al administrador.";
                    return RedirectToPage(new { id = alumno.AlumnoId });
                }

                // Actualizar contraseña
                usuario.Clave = NuevaContrasena;
                _context.Update(usuario);
                await _context.SaveChangesAsync();

                TempData["PasswordSuccessMessage"] = "La contraseña se actualizó correctamente.";
                return RedirectToPage(new { id = alumno.AlumnoId });
            }
            catch (Exception ex)
            {
                TempData["PasswordErrorMessage"] = "Error al actualizar la contraseña: " + ex.Message;
                // Intentar redirigir conservando el id del alumno
                var userIdClaimCatch = User.FindFirstValue("UserId") ?? "0";
                if (int.TryParse(userIdClaimCatch, out var usuarioIdCatch))
                {
                    var alumnoCatch = await _context.Alumno.FirstOrDefaultAsync(a => a.UsuarioId == usuarioIdCatch);
                    if (alumnoCatch != null)
                    {
                        return RedirectToPage(new { id = alumnoCatch.AlumnoId });
                    }
                }
                return RedirectToPage();
            }
        }

        private bool AlumnoExists(int id)
        {
            return _context.Alumno.Any(e => e.AlumnoId == id);
        }

        private List<string> ObtenerDocumentosPendientes(Alumno alumno)
        {
            var pendientes = new List<string>();

            if (!alumno.PPartida)
                pendientes.Add("Partida de Nacimiento");

            if (!alumno.PTitulo)
                pendientes.Add("Título de Bachillerato");

            if (!alumno.PFotografias)
                pendientes.Add("2 Fotografías");

            if (!alumno.PExamenOrina)
                pendientes.Add("Examen de Orina Original");

            if (!alumno.PHemograma)
                pendientes.Add("Examen de Hemograma");

            if (!alumno.PPreuniversitario)
                pendientes.Add("Curso Pre-Universitario");

            if (!alumno.PPaes)
                pendientes.Add("PAES ó AVANZO");

            // Solo mostrar solicitud de equivalencias si el alumno ingresó por equivalencias
            if (alumno.IngresoPorEquivalencias && !alumno.PSolicitudEquivalencia)
                pendientes.Add("Cancelar $10 para solicitud de equivalencias");

            return pendientes;
        }
    }
}
