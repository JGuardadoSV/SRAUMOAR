using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.Autenticacion
{
    public class DetailsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DetailsModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public NivelAcceso NivelAcceso { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nivelacceso = await _context.NivelAcceso.FirstOrDefaultAsync(m => m.Id == id);
            if (nivelacceso == null)
            {
                return NotFound();
            }
            else
            {
                NivelAcceso = nivelacceso;
            }
            return Page();
        }
    }
}
