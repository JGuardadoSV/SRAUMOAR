using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.alumno
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
        public Alumno Alumno { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            ViewData["MunicipioId"] = new SelectList(_context.Municipios, "MunicipioId", "NombreMunicipio");
            var alumno =  await _context.Alumno.FirstOrDefaultAsync(m => m.AlumnoId == id);
            if (alumno == null)
            {
                return NotFound();
            }
            Alumno = alumno;
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

            _context.Attach(Alumno).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AlumnoExists(Alumno.AlumnoId))
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

        private bool AlumnoExists(int id)
        {
            return _context.Alumno.Any(e => e.AlumnoId == id);
        }
    }
}
