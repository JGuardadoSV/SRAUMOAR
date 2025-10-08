using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.codigoActividadEconomica
{
    public class DeleteModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DeleteModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public CodigoActividadEconomica CodigoActividadEconomica { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var codigoActividadEconomica = await _context.CodigosActividadEconomica.FirstOrDefaultAsync(m => m.Id == id);

            if (codigoActividadEconomica == null)
            {
                return NotFound();
            }
            else
            {
                CodigoActividadEconomica = codigoActividadEconomica;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var codigoActividadEconomica = await _context.CodigosActividadEconomica.FindAsync(id);
            if (codigoActividadEconomica != null)
            {
                CodigoActividadEconomica = codigoActividadEconomica;
                _context.CodigosActividadEconomica.Remove(CodigoActividadEconomica);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
