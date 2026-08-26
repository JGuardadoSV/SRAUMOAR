using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.facturas
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class EditModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public Factura Factura { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var factura =  await _context.Facturas.FirstOrDefaultAsync(m => m.FacturaId == id);
            if (factura == null)
            {
                return NotFound();
            }
            if (EsDocumentoFiscalProtegido(factura))
                return BadRequest("Los documentos fiscales no se editan desde el CRUD genérico.");
            Factura = factura;
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var existente = await _context.Facturas.AsNoTracking().SingleOrDefaultAsync(x => x.FacturaId == Factura.FacturaId);
            if (existente is null) return NotFound();
            if (EsDocumentoFiscalProtegido(existente) || EsDocumentoFiscalProtegido(Factura))
                return BadRequest("Los documentos fiscales no se editan desde el CRUD genérico.");

            _context.Attach(Factura).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FacturaExists(Factura.FacturaId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool FacturaExists(int id)
        {
            return _context.Facturas.Any(e => e.FacturaId == id);
        }

        private static bool EsDocumentoFiscalProtegido(Factura factura)
            => !string.IsNullOrWhiteSpace(factura.JsonDte)
                || !string.IsNullOrWhiteSpace(factura.SelloRecepcion)
                || !string.IsNullOrWhiteSpace(factura.JsonAnulacion)
                || !string.IsNullOrWhiteSpace(factura.SelloAnulacion)
                || !string.IsNullOrWhiteSpace(factura.TipoDteCodigo)
                || factura.FacturaOriginalId.HasValue
                || factura.Anulada
                || factura.EstadoTransmision != "Preparado";
    }
}
