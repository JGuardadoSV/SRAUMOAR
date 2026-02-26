using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.facturacion.correlativos
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
            CargarSelectLists();
            return Page();
        }

        [BindProperty]
        public DteCorrelativo DteCorrelativo { get; set; } = default!;

        public SelectList TiposDocumento { get; set; } = default!;
        public SelectList Ambientes { get; set; } = default!;

        private void CargarSelectLists()
        {
            var tiposDocumento = new List<SelectListItem>
            {
                new SelectListItem { Value = "01", Text = "01 - Consumidor Final" },
                new SelectListItem { Value = "03", Text = "03 - Crédito Fiscal" },
                new SelectListItem { Value = "14", Text = "14 - Sujeto Excluido" },
                new SelectListItem { Value = "15", Text = "15 - Donación" }
            };

            var ambientes = new List<SelectListItem>
            {
                new SelectListItem { Value = "00", Text = "00 - Prueba" },
                new SelectListItem { Value = "01", Text = "01 - Producción" }
            };

            TiposDocumento = new SelectList(tiposDocumento, "Value", "Text");
            Ambientes = new SelectList(ambientes, "Value", "Text");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Validar que el correlativo sea mayor a 0
            if (DteCorrelativo.Correlativo <= 0)
            {
                ModelState.AddModelError("DteCorrelativo.Correlativo", "El correlativo debe ser mayor a 0.");
                CargarSelectLists();
                return Page();
            }

            // Validar que no exista ya un correlativo con el mismo TipoDocumento + Ambiente
            var existe = await _context.DteCorrelativos
                .AnyAsync(x => x.TipoDocumento == DteCorrelativo.TipoDocumento 
                    && x.Ambiente == DteCorrelativo.Ambiente);

            if (existe)
            {
                ModelState.AddModelError("", 
                    $"Ya existe un correlativo para el tipo de documento {DteCorrelativo.TipoDocumento} " +
                    $"en el ambiente {DteCorrelativo.Ambiente}. Por favor, edite el existente en lugar de crear uno nuevo.");
                CargarSelectLists();
                return Page();
            }

            if (!ModelState.IsValid)
            {
                CargarSelectLists();
                return Page();
            }

            DteCorrelativo.UltimaActualizacion = DateTime.Now;
            _context.DteCorrelativos.Add(DteCorrelativo);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Correlativo creado exitosamente para tipo de documento {DteCorrelativo.TipoDocumento} en ambiente {DteCorrelativo.Ambiente}.";
            return RedirectToPage("./Index");
        }
    }
}


