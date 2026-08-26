using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.facturas
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class DetailsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IDteRetornoService _retornos;

        public DetailsModel(SRAUMOAR.Modelos.Contexto context, IDteRetornoService retornos)
        {
            _context = context;
            _retornos = retornos;
        }

        public Factura Factura { get; set; } = default!;
        public bool PuedeRetornar { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var factura = await _context.Facturas
                .Include(x => x.DetallesRetorno)
                .Include(x => x.FacturaOriginal)
                .FirstOrDefaultAsync(m => m.FacturaId == id);

            if (factura is not null)
            {
                Factura = factura;
                if (!factura.Anulada && !string.IsNullOrWhiteSpace(factura.SelloRecepcion)
                    && (factura.TipoDTE is 1 or 11 or 14 || factura.TipoDteCodigo is "01" or "11" or "14"))
                {
                    try
                    {
                        var disponible = await _retornos.ObtenerDisponibleAsync(factura.FacturaId);
                        PuedeRetornar = disponible.Lineas.Any(x => x.CantidadDisponible > 0);
                    }
                    catch (InvalidOperationException) { PuedeRetornar = false; }
                }

                return Page();
            }

            return NotFound();
        }
    }
}
