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

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        public async Task OnGetAsync(int? id)
        {
            if (id.HasValue) 
            {
                DistritoFiltradoId = id;
            }

            ViewData["DistritoId"] = new SelectList(
                await _context.Distritos.OrderBy(d => d.NombreDistrito).ToListAsync(),
                "DistritoId",
                "NombreDistrito",
                DistritoFiltradoId);

            var query = _context.Municipios
                .Include(m => m.Distrito)
                .AsQueryable();

            if (DistritoFiltradoId.HasValue && DistritoFiltradoId > 0)
            {
                query = query.Where(x => x.DistritoId == DistritoFiltradoId);
            }

            if (!string.IsNullOrWhiteSpace(SearchString))
            {
                var search = SearchString.Trim();
                query = query.Where(m =>
                    (m.Codigo != null && m.Codigo.Contains(search)) ||
                    (m.NombreMunicipio != null && m.NombreMunicipio.Contains(search)) ||
                    (m.Distrito != null && m.Distrito.NombreDistrito != null && m.Distrito.NombreDistrito.Contains(search)));
            }

            Municipio = await query
                .OrderBy(m => m.Distrito!.NombreDistrito)
                .ThenBy(m => m.NombreMunicipio)
                .ToListAsync();
        }
    }
}
