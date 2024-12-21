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

namespace SRAUMOAR.Pages.entidadesBecas
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
        public EntidadBeca EntidadBeca { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entidadbeca = await _context.InstitucionesBeca.FirstOrDefaultAsync(m => m.EntidadBecaId == id);

            if (entidadbeca is not null)
            {
                EntidadBeca = entidadbeca;

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

            var entidadbeca = await _context.InstitucionesBeca.FindAsync(id);
            if (entidadbeca != null)
            {
                EntidadBeca = entidadbeca;
                _context.InstitucionesBeca.Remove(EntidadBeca);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
