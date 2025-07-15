using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Colecturia;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SRAUMOAR.Pages.aranceles
{
    public class HistorialPagosModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        public HistorialPagosModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public List<DetallesCobroArancel> Pagos { get; set; } = new List<DetallesCobroArancel>();

        public async Task<IActionResult> OnGetAsync(int? alumnoId)
        {
            if (alumnoId == null)
                return NotFound();

            var alumno = await _context.Alumno.FirstOrDefaultAsync(a => a.AlumnoId == alumnoId);
            ViewData["Alumno"] = alumno;

            Pagos = await _context.DetallesCobrosArancel
                .Include(d => d.Arancel).ThenInclude(a => a.Ciclo)
                .Include(d => d.CobroArancel)
                .Where(d => d.CobroArancel.AlumnoId == alumnoId)
                .OrderByDescending(d => d.CobroArancel.Fecha)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostEliminarPagoAsync(string codigoGeneracion, int? alumnoId)
        {
            if (string.IsNullOrEmpty(codigoGeneracion) || alumnoId == null)
            {
                ModelState.AddModelError(string.Empty, "Faltan parámetros requeridos para eliminar el pago.");
                await OnGetAsync(alumnoId);
                return Page();
            }

            // Buscar el cobro principal por CodigoGeneracion
            var cobro = await _context.CobrosArancel
                .Include(c => c.DetallesCobroArancel)
                .FirstOrDefaultAsync(c => c.CodigoGeneracion == codigoGeneracion && c.AlumnoId == alumnoId);

            if (cobro != null)
            {
                // Eliminar todos los detalles asociados
                _context.DetallesCobrosArancel.RemoveRange(cobro.DetallesCobroArancel);
                // Eliminar el cobro principal
                _context.CobrosArancel.Remove(cobro);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { alumnoId = alumnoId.Value });
        }
    }
} 