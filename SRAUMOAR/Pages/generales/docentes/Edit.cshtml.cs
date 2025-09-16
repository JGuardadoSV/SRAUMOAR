using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.docentes
{
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Docente Docente { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var docente =  await _context.Docentes.FirstOrDefaultAsync(m => m.DocenteId == id);
            if (docente == null)
            {
                return NotFound();
            }
            Docente = docente;
           ViewData["ProfesionId"] = new SelectList(_context.Profesiones, "ProfesionId", "NombreProfesion");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Cargar el docente existente para detectar cambios y obtener el UsuarioId real
            var existingDocente = await _context.Docentes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DocenteId == Docente.DocenteId);

            if (existingDocente == null)
            {
                return NotFound();
            }

            bool emailChanged = !string.Equals(existingDocente.Email, Docente.Email, StringComparison.OrdinalIgnoreCase);

            _context.Attach(Docente).State = EntityState.Modified;
            // Evitar que se sobreescriba el UsuarioId si no viene en el formulario
            _context.Entry(Docente).Property(d => d.UsuarioId).IsModified = false;
            // Evitar modificar campos de solo registro
            _context.Entry(Docente).Property(d => d.FechaDeRegistro).IsModified = false;

            // Si cambió el email y el docente tiene usuario vinculado, sincronizar con Usuarios
            if (emailChanged && existingDocente.UsuarioId.HasValue)
            {
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.IdUsuario == existingDocente.UsuarioId.Value);

                if (usuario != null)
                {
                    usuario.Email = Docente.Email;
                    usuario.NombreUsuario = Docente.Email;
                    // El usuario ya está siendo trackeado tras la consulta, no es necesario Attach/Update explícito
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DocenteExists(Docente.DocenteId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool DocenteExists(int id)
        {
            return _context.Docentes.Any(e => e.DocenteId == id);
        }
    }
}
