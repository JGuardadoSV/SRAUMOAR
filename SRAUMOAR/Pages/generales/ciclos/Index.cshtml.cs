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
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Ciclo> Ciclo { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Ciclo = await _context.Ciclos.OrderByDescending(x=>x.Id).ToListAsync();
        }

        public async Task<IActionResult> OnPostActivarAsync(int id)
        {
            var ciclo = await _context.Ciclos.FirstOrDefaultAsync(x => x.Id == id);
            if (ciclo == null)
            {
                return NotFound();
            }

            var existeOtroActivo = await _context.Ciclos.AnyAsync(x => x.Activo && x.Id != id);
            if (existeOtroActivo)
            {
                TempData["Error"] = "No se puede activar este ciclo porque ya existe un ciclo activo. Finalice el ciclo actual antes de activar uno nuevo.";
                return RedirectToPage();
            }

            ciclo.Activo = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Ciclo activado correctamente.";
            return RedirectToPage();
        }
    }
}
