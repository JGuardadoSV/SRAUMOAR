using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.donantes
{
    public class DetailsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DetailsModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public Donantes Donantes { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var donantes = await _context.Donantes.FirstOrDefaultAsync(m => m.Id == id);

            if (donantes is not null)
            {
                Donantes = donantes;

                return Page();
            }

            return NotFound();
        }
    }
}
