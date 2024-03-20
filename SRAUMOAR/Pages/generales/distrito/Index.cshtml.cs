using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.distrito
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Distrito> Distrito { get;set; } = default!;

        public async Task OnGetAsync(int? id)
        {
            if (id.HasValue && id>0)
            {
                Distrito = await _context.Distritos
                    .Include(d => d.Departamento).Where(x=>x.DepartamentoId==id).ToListAsync();
            }
            else
            {
                Distrito = await _context.Distritos
                    .Include(d => d.Departamento).ToListAsync();
            }
           
        }
    }
}
