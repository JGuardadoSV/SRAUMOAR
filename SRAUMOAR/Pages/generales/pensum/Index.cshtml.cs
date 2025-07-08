using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.pensum
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Pensum> Pensum { get;set; } = default!;

        public async Task OnGetAsync(int? id)
        {
            if (id == null)
            {
                Pensum = await _context.Pensums
               .Include(p => p.Carrera).ToListAsync();
            }
            else
            {
                Pensum = await _context.Pensums
                .Include(p => p.Carrera)
                .Where(p => p.CarreraId == id)
                .ToListAsync();
            }
           
        }
    }
}
