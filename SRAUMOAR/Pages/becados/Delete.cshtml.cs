using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.becados
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
        public Becados Becados { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var becados = await _context.Becados.FirstOrDefaultAsync(m => m.BecadosId == id);

            if (becados is not null)
            {
                Becados = becados;

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

            var becados = await _context.Becados.FindAsync(id);
            if (becados != null)
            {
                Becados = becados;
                _context.Becados.Remove(Becados);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
