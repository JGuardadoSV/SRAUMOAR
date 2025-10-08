using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.facturas.procesar
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public IndexModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IList<Factura> FacturasPendientes { get; set; } = default!;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public int PaginaActual { get; set; } = 1;

        public int TotalPaginas { get; set; }
        public int TotalRegistros { get; set; }
        public int RegistrosPorPagina { get; set; } = 20;

        public async Task OnGetAsync()
        {
            var query = _context.Facturas
                .Where(f => string.IsNullOrEmpty(f.SelloRecepcion) && !f.Anulada)
                .AsQueryable();

            // Filtro por fechas
            if (FechaInicio.HasValue)
            {
                query = query.Where(f => f.Fecha >= FechaInicio.Value);
            }

            if (FechaFin.HasValue)
            {
                query = query.Where(f => f.Fecha <= FechaFin.Value.AddDays(1).AddSeconds(-1));
            }

            // Calcular total de registros
            TotalRegistros = await query.CountAsync();
            TotalPaginas = (int)Math.Ceiling((double)TotalRegistros / RegistrosPorPagina);

            // Aplicar paginación
            var facturasPaginadas = await query
                .OrderByDescending(f => f.Fecha)
                .Skip((PaginaActual - 1) * RegistrosPorPagina)
                .Take(RegistrosPorPagina)
                .ToListAsync();

            FacturasPendientes = facturasPaginadas;
        }

        // Método para obtener el texto del tipo DTE
        private string ObtenerTipoDTETexto(int tipoDTE)
        {
            return tipoDTE switch
            {
                1 => "CF",
                3 => "CCF",
                5 => "NC",
                14 => "SE",
                15 => "DON",
                _ => tipoDTE.ToString()
            };
        }
    }
}
