using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.facturacion.correlativos
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
        public DteCorrelativo DteCorrelativo { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var correlativo = await _context.DteCorrelativos.FirstOrDefaultAsync(m => m.Id == id);
            
            if (correlativo == null)
            {
                return NotFound();
            }
            
            DteCorrelativo = correlativo;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Validar que el correlativo sea mayor a 0
            if (DteCorrelativo.Correlativo <= 0)
            {
                ModelState.AddModelError("DteCorrelativo.Correlativo", "El correlativo debe ser mayor a 0.");
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Actualizar solo el correlativo y la fecha de actualización
            var correlativoExistente = await _context.DteCorrelativos
                .FirstOrDefaultAsync(m => m.Id == DteCorrelativo.Id);

            if (correlativoExistente == null)
            {
                return NotFound();
            }

            correlativoExistente.Correlativo = DteCorrelativo.Correlativo;
            correlativoExistente.UltimaActualizacion = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CorrelativoExists(DteCorrelativo.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            TempData["SuccessMessage"] = $"Correlativo actualizado exitosamente. Nuevo valor: {DteCorrelativo.Correlativo}";
            return RedirectToPage("./Index");
        }

        private bool CorrelativoExists(int id)
        {
            return _context.DteCorrelativos.Any(e => e.Id == id);
        }

        public string ObtenerDescripcionTipoDocumento(string tipoDocumento)
        {
            return tipoDocumento switch
            {
                "01" => "Consumidor Final",
                "03" => "Crédito Fiscal",
                "14" => "Sujeto Excluido",
                "15" => "Donación",
                _ => tipoDocumento
            };
        }

        public string ObtenerDescripcionAmbiente(string ambiente)
        {
            return ambiente switch
            {
                "00" => "Prueba",
                "01" => "Producción",
                _ => ambiente
            };
        }
    }
}


