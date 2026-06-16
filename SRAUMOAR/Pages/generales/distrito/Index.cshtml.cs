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

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DepartamentoFiltradoId { get; set; }

        public async Task OnGetAsync(int? id)
        {
            if (id.HasValue && id > 0)
            {
                DepartamentoFiltradoId = id;
            }

            ViewData["DepartamentoId"] = new SelectList(
                await _context.Departamentos.OrderBy(d => d.NombreDepartamento).ToListAsync(),
                "DepartamentoId",
                "NombreDepartamento",
                DepartamentoFiltradoId);

            var query = _context.Distritos
                .Include(d => d.Departamento)
                .AsQueryable();

            if (DepartamentoFiltradoId.HasValue && DepartamentoFiltradoId > 0)
            {
                query = query.Where(x => x.DepartamentoId == DepartamentoFiltradoId);
            }

            if (!string.IsNullOrWhiteSpace(SearchString))
            {
                var search = SearchString.Trim();
                query = query.Where(d =>
                    (d.NombreDistrito != null && d.NombreDistrito.Contains(search)) ||
                    (d.Departamento != null && d.Departamento.NombreDepartamento != null && d.Departamento.NombreDepartamento.Contains(search)));
            }

            Distrito = await query
                .OrderBy(d => d.Departamento!.NombreDepartamento)
                .ThenBy(d => d.NombreDistrito)
                .ToListAsync();
        }
    }
}
