using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.facturas
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class CreateModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public CreateModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public Factura Factura { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(Factura.JsonDte)
                || !string.IsNullOrWhiteSpace(Factura.SelloRecepcion)
                || !string.IsNullOrWhiteSpace(Factura.JsonAnulacion)
                || !string.IsNullOrWhiteSpace(Factura.SelloAnulacion)
                || !string.IsNullOrWhiteSpace(Factura.TipoDteCodigo)
                || Factura.FacturaOriginalId.HasValue
                || Factura.TipoDTE == 18
                || Factura.Anulada
                || Factura.EstadoTransmision != "Preparado")
                return BadRequest("Los documentos fiscales solo pueden crearse desde su flujo controlado de emisión.");

            _context.Facturas.Add(Factura);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
