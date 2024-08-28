using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Arancel> Arancel { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Ciclo cicloactual=await _context.Ciclos.Where(x=>x.Activo).FirstAsync();
            Arancel = await _context.Aranceles.Where(x => x.Ciclo.Id == cicloactual.Id)
                .Include(a => a.Ciclo).ToListAsync();
        }
    }
}
