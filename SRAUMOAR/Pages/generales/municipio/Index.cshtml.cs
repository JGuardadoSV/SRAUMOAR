using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.municipio
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Municipio> Municipio { get;set; } = default!;

        public async Task OnGetAsync(int? id)
        {
            if (id.HasValue && id > 0)
            {
                Municipio = await _context.Municipios
                    .Include(m => m.Distrito).Where(x=>x.DistritoId==id).ToListAsync();
            }
            else
            {
                Municipio = await _context.Municipios
                    .Include(m => m.Distrito).ToListAsync();
            }
           
        }
    }
}
