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

        private bool AlumnoExists(int id)
        {
            return _context.Alumno.Any(e => e.AlumnoId == id);
        }
    }
}
