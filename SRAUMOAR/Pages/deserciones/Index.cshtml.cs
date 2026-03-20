using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.deserciones
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;
        private readonly ReporteDesercionesService _reporteDesercionesService;

        public IndexModel(Contexto context, ReporteDesercionesService reporteDesercionesService)
        {
            _context = context;
            _reporteDesercionesService = reporteDesercionesService;
        }

        [BindProperty(SupportsGet = true)]
        public int? SelectedCicloId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedCausaDesercionId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Buscar { get; set; }

        public List<DesercionListadoItem> Deserciones { get; set; } = new();
        public List<ResumenCausaItem> ResumenPorCausa { get; set; } = new();
        public int TotalRegistros { get; set; }

        public async Task OnGetAsync()
        {
            await CargarFiltrosAsync();
            await CargarDatosAsync();
        }

        public async Task<IActionResult> OnGetGenerarExcelAsync(int cicloId, int? causaDesercionId)
        {
            try
            {
                var archivo = await _reporteDesercionesService.GenerarReporteExcelAsync(cicloId, causaDesercionId);
                return File(
                    archivo,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Reporte_Deserciones_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToPage(new { SelectedCicloId = cicloId, SelectedCausaDesercionId = causaDesercionId });
            }
        }

        private async Task CargarFiltrosAsync()
        {
            if (!SelectedCicloId.HasValue || SelectedCicloId.Value <= 0)
            {
                SelectedCicloId = await _context.Ciclos
                    .AsNoTracking()
                    .Where(c => c.Activo)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync()
                    ?? await _context.Ciclos
                        .AsNoTracking()
                        .OrderByDescending(c => c.anio)
                        .ThenByDescending(c => c.NCiclo)
                        .Select(c => (int?)c.Id)
                        .FirstOrDefaultAsync();
            }

            ViewData["Ciclos"] = new SelectList(
                await _context.Ciclos
                    .AsNoTracking()
                    .OrderByDescending(c => c.anio)
                    .ThenByDescending(c => c.NCiclo)
                    .Select(c => new
                    {
                        c.Id,
                        Nombre = $"Ciclo {c.NCiclo}/{c.anio}" + (c.Activo ? " (Activo)" : "")
                    })
                    .ToListAsync(),
                "Id",
                "Nombre",
                SelectedCicloId);

            ViewData["CausasDesercion"] = new SelectList(
                await _context.CausasDesercion
                    .AsNoTracking()
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Nombre)
                    .ToListAsync(),
                "CausaDesercionId",
                "Nombre",
                SelectedCausaDesercionId);
        }

        private async Task CargarDatosAsync()
        {
            if (!SelectedCicloId.HasValue || SelectedCicloId.Value <= 0)
            {
                Deserciones = new List<DesercionListadoItem>();
                ResumenPorCausa = new List<ResumenCausaItem>();
                return;
            }

            var query = _context.DesercionesAlumno
                .AsNoTracking()
                .Include(d => d.Alumno)
                    .ThenInclude(a => a!.Carrera)
                .Include(d => d.CausaDesercion)
                .Include(d => d.Ciclo)
                .Where(d => d.CicloId == SelectedCicloId.Value);

            if (SelectedCausaDesercionId.HasValue && SelectedCausaDesercionId.Value > 0)
            {
                query = query.Where(d => d.CausaDesercionId == SelectedCausaDesercionId.Value);
            }

            if (!string.IsNullOrWhiteSpace(Buscar))
            {
                var termino = Buscar.Trim();
                query = query.Where(d =>
                    (d.Alumno!.Nombres != null && d.Alumno.Nombres.Contains(termino)) ||
                    (d.Alumno.Apellidos != null && d.Alumno.Apellidos.Contains(termino)) ||
                    (d.Alumno.Carnet != null && d.Alumno.Carnet.Contains(termino)) ||
                    (d.Alumno.Email != null && d.Alumno.Email.Contains(termino)));
            }

            var desercionesDb = await query
                .OrderBy(d => d.Alumno!.Apellidos)
                .ThenBy(d => d.Alumno!.Nombres)
                .ToListAsync();

            Deserciones = desercionesDb
                .Select(d => new DesercionListadoItem
                {
                    DesercionAlumnoId = d.DesercionAlumnoId,
                    AlumnoId = d.AlumnoId,
                    Carnet = !string.IsNullOrWhiteSpace(d.Alumno?.Carnet)
                        ? d.Alumno.Carnet
                        : (d.Alumno?.Email != null && d.Alumno.Email.Contains('@') ? d.Alumno.Email.Split('@')[0] : string.Empty),
                    Alumno = $"{d.Alumno?.Apellidos}, {d.Alumno?.Nombres}",
                    Carrera = d.Alumno?.Carrera?.NombreCarrera ?? string.Empty,
                    Causa = d.CausaDesercion?.Nombre ?? string.Empty,
                    Observacion = d.Observacion,
                    FechaRegistro = d.FechaRegistro,
                    CicloNombre = d.Ciclo != null ? $"{d.Ciclo.NCiclo}/{d.Ciclo.anio}" : string.Empty
                })
                .ToList();

            TotalRegistros = Deserciones.Count;
            ResumenPorCausa = Deserciones
                .GroupBy(d => d.Causa)
                .OrderBy(g => g.Key)
                .Select(g => new ResumenCausaItem
                {
                    Causa = g.Key,
                    Cantidad = g.Count()
                })
                .ToList();
        }

        public class DesercionListadoItem
        {
            public int DesercionAlumnoId { get; set; }
            public int AlumnoId { get; set; }
            public string Carnet { get; set; } = string.Empty;
            public string Alumno { get; set; } = string.Empty;
            public string Carrera { get; set; } = string.Empty;
            public string Causa { get; set; } = string.Empty;
            public string? Observacion { get; set; }
            public DateTime FechaRegistro { get; set; }
            public string CicloNombre { get; set; } = string.Empty;
        }

        public class ResumenCausaItem
        {
            public string Causa { get; set; } = string.Empty;
            public int Cantidad { get; set; }
        }
    }
}
