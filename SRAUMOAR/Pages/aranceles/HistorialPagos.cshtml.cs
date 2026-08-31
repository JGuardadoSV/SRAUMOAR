using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
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
            if (!TienePermiso()) return Forbid();
            await CargarHistorialAsync(AlumnoId);
            return Page();
        }

        public async Task<IActionResult> OnPostBuscarAlumnoAsync()
        {
            if (!TienePermiso()) return Forbid();
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

        private bool TienePermiso()
            => User.Identity?.IsAuthenticated == true
                && (User.IsInRole("Administrador") || User.IsInRole("Administracion") || User.IsInRole("Contabilidad"));

        public Task<IActionResult> OnPostEliminarPagoAsync(string codigoGeneracion, int? alumnoId)
        {
            if (!TienePermiso()) return Task.FromResult<IActionResult>(Forbid());

            // Este endpoint histórico solo recibe un código, no una relación fiscal
            // verificable. No inferir que es seguro borrar por código vacío/inválido,
            // factura no encontrada o estado rechazado/anulado. Conservar el cobro
            // y sus detalles; la corrección debe pasar por el flujo fiscal auditable.
            return Task.FromResult<IActionResult>(BadRequest(
                "No se permite eliminar pagos desde el historial. Utilice la ruta fiscal de invalidación o nota de crédito del DTE, según corresponda; el cobro y sus detalles deben conservarse."));
        }
    }
}
