using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Becas;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.entidadesBecas
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<EntidadBeca> EntidadBeca { get;set; } = default!;

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.InstitucionesBeca.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchString))
            {
                var search = SearchString.Trim();
                query = query.Where(e =>
                    (e.Nombre != null && e.Nombre.Contains(search)) ||
                    (e.Siglas != null && e.Siglas.Contains(search)) ||
                    (e.NombreRepresente != null && e.NombreRepresente.Contains(search)) ||
                    (e.Telefono != null && e.Telefono.Contains(search)) ||
                    (e.Email != null && e.Email.Contains(search)));
            }

            EntidadBeca = await query
                .OrderBy(e => e.Nombre)
                .ToListAsync();
        }
    }
}
