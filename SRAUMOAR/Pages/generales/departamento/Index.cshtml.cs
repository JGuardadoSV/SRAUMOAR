using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.departamento
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Departamento> Departamento { get;set; } = default!;

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.Departamentos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchString))
            {
                var search = SearchString.Trim();
                query = query.Where(d => d.NombreDepartamento != null && d.NombreDepartamento.Contains(search));
            }

            Departamento = await query
                .OrderBy(d => d.NombreDepartamento)
                .ToListAsync();
        }
    }
}
