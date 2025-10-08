using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.codigoActividadEconomica
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public PaginatedList<CodigoActividadEconomica> CodigoActividadEconomica { get; set; } = default!;
        
        [BindProperty(SupportsGet = true)]
        public string? FiltroBusqueda { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? OrdenActual { get; set; }

        public async Task OnGetAsync(string? filtroBusqueda, string? ordenActual, int? numeroPagina)
        {
            FiltroBusqueda = filtroBusqueda;
            OrdenActual = ordenActual;

            var codigos = from c in _context.CodigosActividadEconomica
                         select c;

            // Aplicar filtro de búsqueda
            if (!string.IsNullOrEmpty(FiltroBusqueda))
            {
                codigos = codigos.Where(c => 
                    c.Codigo!.Contains(FiltroBusqueda) || 
                    c.Descripcion!.Contains(FiltroBusqueda));
            }

            // Aplicar ordenamiento
            codigos = OrdenActual switch
            {
                "codigo_desc" => codigos.OrderByDescending(c => c.Codigo),
                "descripcion" => codigos.OrderBy(c => c.Descripcion),
                "descripcion_desc" => codigos.OrderByDescending(c => c.Descripcion),
                _ => codigos.OrderBy(c => c.Codigo),
            };

            CodigoActividadEconomica = await PaginatedList<CodigoActividadEconomica>.CreateAsync(
                codigos.AsNoTracking(), numeroPagina ?? 1, 10);
        }
    }
}
