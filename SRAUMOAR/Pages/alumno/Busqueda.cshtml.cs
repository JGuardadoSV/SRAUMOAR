using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.alumno
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class BusquedaModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public BusquedaModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Alumno> Alumno { get; set; } = default!;
        public string busqueda { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        
        public int PageSize { get; set; } = 10;
        
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public async Task OnGetAsync()
        {
            this.busqueda = Request.Query["buscar"].ToString();
            PageNumber = Math.Max(1, PageNumber);
            
            var query = _context.Alumno
                .Include(a => a.Usuario)
                .Where(a => a.Nombres.Contains(busqueda) || 
                           a.Apellidos.Contains(busqueda) || 
                           a.TelefonoPrimario.Contains(busqueda));

            TotalItems = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);

            if (TotalPages > 0 && PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
            }

            Alumno = await query
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
    }
}
