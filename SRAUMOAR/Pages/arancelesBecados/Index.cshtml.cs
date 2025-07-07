using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Becas;

namespace SRAUMOAR.Pages.arancelesBecados
{
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;

        public IndexModel(Contexto context)
        {
            _context = context;
        }

        public IList<ArancelBecado> ArancelesBecados { get; set; } = default!;

        public async Task OnGetAsync()
        {
            ArancelesBecados = await _context.ArancelesBecados
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.Alumno)
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.EntidadBeca)
                .Include(ab => ab.Becado)
                    .ThenInclude(b => b.Ciclo)
                .Include(ab => ab.Arancel)
                .Where(ab => ab.Activo)
                .OrderByDescending(ab => ab.FechaRegistro)
                .ToListAsync();
        }
    }
} 