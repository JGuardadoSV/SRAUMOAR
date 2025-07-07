using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.becados
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Becados Becados { get; set; } = default!;

        // Propiedades para mostrar información del alumno
        public string NombreAlumno { get; set; } = "";
        public string CarreraAlumno { get; set; } = "";

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var becados = await _context.Becados
                .Include(b => b.Alumno)
                .ThenInclude(a => a.Carrera)
                .FirstOrDefaultAsync(m => m.BecadosId == id);
                
            if (becados == null)
            {
                return NotFound();
            }
            
            Becados = becados;
            
            // Cargar información del alumno para mostrar
            if (becados.Alumno != null)
            {
                NombreAlumno = $"{becados.Alumno.Apellidos}, {becados.Alumno.Nombres}";
                CarreraAlumno = becados.Alumno.Carrera?.NombreCarrera ?? "No especificada";
            }

            // Solo cargar los dropdowns necesarios (no el de alumnos)
            ViewData["CicloId"] = new SelectList(_context.Ciclos
                .Select(c => new {
                    Id = c.Id,
                    Display = $"Ciclo {c.NCiclo} - {c.anio}"
                }), "Id", "Display");
                
            ViewData["EntidadBecaId"] = new SelectList(_context.InstitucionesBeca, "EntidadBecaId", "Nombre");
            
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                // Recargar los datos necesarios si hay errores de validación
                ViewData["CicloId"] = new SelectList(_context.Ciclos
                    .Select(c => new {
                        Id = c.Id,
                        Display = $"Ciclo {c.NCiclo} - {c.anio}"
                    }), "Id", "Display");
                    
                ViewData["EntidadBecaId"] = new SelectList(_context.InstitucionesBeca, "EntidadBecaId", "Nombre");
                
                return Page();
            }

            _context.Attach(Becados).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BecadosExists(Becados.BecadosId))
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

        private bool BecadosExists(int id)
        {
            return _context.Becados.Any(e => e.BecadosId == id);
        }
    }
}
