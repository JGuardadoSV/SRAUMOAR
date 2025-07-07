using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Becas;

namespace SRAUMOAR.Pages.arancelesBecados
{
    public class DetailsModel : PageModel
    {
        private readonly Contexto _context;

        public DetailsModel(Contexto context)
        {
            _context = context;
        }

        public ArancelBecado ArancelBecado { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var arancelBecado = await _context.ArancelesBecados
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.Alumno)
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.EntidadBeca)
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.Ciclo)
                .Include(ab => ab.Arancel)
                .FirstOrDefaultAsync(ab => ab.ArancelBecadoId == id);

            if (arancelBecado == null)
            {
                return NotFound();
            }

            ArancelBecado = arancelBecado;
            return Page();
        }
    }
} 