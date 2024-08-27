using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.ciclos
{
    public class DeleteModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public DeleteModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Ciclo Ciclo { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ciclo = await _context.Ciclos.FirstOrDefaultAsync(m => m.Id == id);

            if (ciclo == null)
            {
                return NotFound();
            }
            else
            {
                Ciclo = ciclo;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ciclo = await _context.Ciclos.FindAsync(id);
            if (ciclo != null)
            {
                Ciclo = ciclo;
                _context.Ciclos.Remove(Ciclo);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
