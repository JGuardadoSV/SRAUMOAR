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

        [BindProperty(SupportsGet = true)]
        public int? AlumnoId { get; set; }

        [BindProperty]
        public string? FiltroAlumno { get; set; }

        public List<Alumno> ResultadosBusqueda { get; set; } = new List<Alumno>();

        public async Task<IActionResult> OnGetAsync()
        {
            await CargarHistorialAsync(AlumnoId);
            return Page();
        }

        public async Task<IActionResult> OnPostBuscarAlumnoAsync()
        {
            // Búsqueda de alumnos por nombre, apellido o carnet
            ResultadosBusqueda = new List<Alumno>();

            if (!string.IsNullOrWhiteSpace(FiltroAlumno))
            {
                var filtro = FiltroAlumno.Trim();

                ResultadosBusqueda = await _context.Alumno
                    .Where(a =>
                        (a.Nombres != null && a.Nombres.Contains(filtro)) ||
                        (a.Apellidos != null && a.Apellidos.Contains(filtro)) ||
                        (a.Carnet != null && a.Carnet.Contains(filtro)))
                    .OrderBy(a => a.Apellidos)
                    .ThenBy(a => a.Nombres)
                    .Take(25)
                    .ToListAsync();
            }

            // Mantener el historial del alumno actual si AlumnoId sigue presente
            await CargarHistorialAsync(AlumnoId);

            return Page();
        }

        private async Task CargarHistorialAsync(int? alumnoId)
        {
            if (alumnoId == null)
            {
                ViewData["Alumno"] = null;
                Pagos = new List<DetallesCobroArancel>();
                return;
            }

            var alumno = await _context.Alumno.FirstOrDefaultAsync(a => a.AlumnoId == alumnoId);
            if (alumno == null)
            {
                ViewData["Alumno"] = null;
                Pagos = new List<DetallesCobroArancel>();
                return;
            }

            ViewData["Alumno"] = alumno;

            Pagos = await _context.DetallesCobrosArancel
                .Include(d => d.Arancel).ThenInclude(a => a.Ciclo)
                .Include(d => d.CobroArancel)
                .Where(d => d.CobroArancel.AlumnoId == alumnoId)
                .OrderByDescending(d => d.CobroArancel.Fecha)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostEliminarPagoAsync(string codigoGeneracion, int? alumnoId)
        {
            if (string.IsNullOrEmpty(codigoGeneracion) || alumnoId == null)
            {
                ModelState.AddModelError(string.Empty, "Faltan parámetros requeridos para eliminar el pago.");
                AlumnoId = alumnoId;
                await CargarHistorialAsync(AlumnoId);
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