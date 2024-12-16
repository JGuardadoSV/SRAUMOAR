using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.aranceles
{
    public class FacturasModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public FacturasModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }
        [BindProperty]
        public IList<CobroArancel> CobroArancel { get;set; } = default!;

        public async Task OnGetAsync(int arancelId=0, int alumnoId=0)
        {
            //if (alumnoId != 0 && arancelId != 0) {
            //    CobroArancel = await _context.CobrosArancel
            //    .Include(c => c.Alumno)
            //    .Include(c => c.Arancel)
            //    .Include(c => c.Ciclo)
            //    .Where(c => c.ArancelId == arancelId && c.AlumnoId == alumnoId)
            //    .ToListAsync();
            //}
            //else { 
            
            //CobroArancel = await _context.CobrosArancel
            //    .Include(c => c.Alumno)
            //    .Include(c => c.Arancel)
            //    .Include(c => c.Ciclo)
            //    .OrderByDescending(c => c.CobroArancelId)
            //    .ToListAsync();
            //}
        }
    }
}
