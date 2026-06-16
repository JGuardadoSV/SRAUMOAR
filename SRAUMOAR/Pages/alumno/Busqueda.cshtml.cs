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
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
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
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var terminos = busqueda
                    .Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var termino in terminos)
                {
                    var texto = termino;
                    query = query.Where(a =>
                        (a.Nombres != null && a.Nombres.Contains(texto)) ||
                        (a.Apellidos != null && a.Apellidos.Contains(texto)) ||
                        (a.Carnet != null && a.Carnet.Contains(texto)) ||
                        (a.Email != null && a.Email.Contains(texto)) ||
                        (a.TelefonoPrimario != null && a.TelefonoPrimario.Contains(texto)));
                }
            }

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

