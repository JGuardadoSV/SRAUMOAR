using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.actividades
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class DeleteModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DeleteModel(SRAUMOAR.Modelos.Contexto context)
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

            var actividadacademica = await _context.ActividadesAcademicas.FirstOrDefaultAsync(m => m.ActividadAcademicaId == id);

            if (actividadacademica is not null)
            {
                ActividadAcademica = actividadacademica;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var actividadacademica = await _context.ActividadesAcademicas.FindAsync(id);
            if (actividadacademica != null)
            {
                ActividadAcademica = actividadacademica;
                _context.ActividadesAcademicas.Remove(ActividadAcademica);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
