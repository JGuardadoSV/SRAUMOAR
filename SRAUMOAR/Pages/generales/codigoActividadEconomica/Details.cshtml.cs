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
    public class DetailsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DetailsModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

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
    }
}
