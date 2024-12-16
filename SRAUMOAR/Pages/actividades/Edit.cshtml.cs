using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.actividades
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
        public ActividadAcademica ActividadAcademica { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var actividadacademica =  await _context.ActividadesAcademicas.FirstOrDefaultAsync(m => m.ActividadAcademicaId == id);
            if (actividadacademica == null)
            {
                return NotFound();
            }
            ActividadAcademica = actividadacademica;
            ViewData["ArancelId"] = new SelectList(_context.Aranceles, "ArancelId", "Nombre");
            ViewData["CicloId"] = new SelectList(
        _context.Ciclos.Where(c => c.Activo == true).Select(c => new {
            c.Id,
            Descripcion = $"Ciclo {c.NCiclo} - {c.anio}"
        }),
        "Id",
        "Descripcion"
    );
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(ActividadAcademica).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ActividadAcademicaExists(ActividadAcademica.ActividadAcademicaId))
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

        private bool ActividadAcademicaExists(int id)
        {
            return _context.ActividadesAcademicas.Any(e => e.ActividadAcademicaId == id);
        }
    }
}
