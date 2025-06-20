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
    public class DeleteModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DeleteModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
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

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var donantes = await _context.Donantes.FindAsync(id);
            if (donantes != null)
            {
                Donantes = donantes;
                _context.Donantes.Remove(Donantes);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
