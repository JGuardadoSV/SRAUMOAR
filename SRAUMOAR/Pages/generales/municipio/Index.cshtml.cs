using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        [BindProperty(SupportsGet = true)]
        public int? DistritoFiltradoId { get; set; }

        public async Task OnGetAsync(int? id)
        {
            // Set the bound property if 'id' is passed directly via routing or query matching 'id' param
            if (id.HasValue) 
            {
                DistritoFiltradoId = id;
            }

            // Populate the dropdown list for the filter
            ViewData["DistritoId"] = new SelectList(_context.Distritos.OrderBy(d => d.NombreDistrito), "DistritoId", "NombreDistrito", DistritoFiltradoId);

            if (DistritoFiltradoId.HasValue && DistritoFiltradoId > 0)
            {
                Municipio = await _context.Municipios
                    .Include(m => m.Distrito)
                    .Where(x => x.DistritoId == DistritoFiltradoId)
                    .OrderBy(m => m.NombreMunicipio)
                    .ToListAsync();
            }
            else
            {
                Municipio = await _context.Municipios
                    .Include(m => m.Distrito)
                    .OrderBy(m => m.NombreMunicipio)
                    .ToListAsync();
            }
        }
    }
}
