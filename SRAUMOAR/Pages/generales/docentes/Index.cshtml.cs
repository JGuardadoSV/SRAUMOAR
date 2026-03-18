using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.generales.docentes
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Docente> Docente { get;set; } = default!;
        
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        
        public int PageSize { get; set; } = 10;
        
        public int TotalPages { get; set; }
        
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public async Task OnGetAsync()
        {
            var query = _context.Docentes
                .Include(u => u.Usuario)
                .Include(d => d.Profesion)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                query = query.Where(d =>
                    (d.Nombres != null && d.Nombres.Contains(term)) ||
                    (d.Apellidos != null && d.Apellidos.Contains(term)) ||
                    (d.Dui != null && d.Dui.Contains(term)) ||
                    (d.Email != null && d.Email.Contains(term)));
            }

            var totalItems = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

            Docente = await query
                .OrderBy(d => d.Apellidos)
                .ThenBy(d => d.Nombres)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}
