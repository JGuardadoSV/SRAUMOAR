using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.inscripcion
{
    public class DashboardModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly ReporteInscripcionesService _reporteService;
        private readonly ReporteCuadroEstadisticoService _reporteCuadroEstadisticoService;
        private readonly ReporteInscripcionesExcelService _reporteInscripcionesExcelService;
        
        public DashboardModel(
            SRAUMOAR.Modelos.Contexto context,
            ReporteInscripcionesService reporteService,
            ReporteCuadroEstadisticoService reporteCuadroEstadisticoService,
            ReporteInscripcionesExcelService reporteInscripcionesExcelService)
        {
            _context = context;
            _reporteService = reporteService;
            _reporteCuadroEstadisticoService = reporteCuadroEstadisticoService;
            _reporteInscripcionesExcelService = reporteInscripcionesExcelService;
        }

        public List<SelectListItem> Ciclos { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Carreras { get; set; } = new List<SelectListItem>();
        public IList<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
        [BindProperty(SupportsGet = true)]
        public int? SelectedCicloId { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? SelectedCarreraId { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Genero { get; set; }
        public int TotalInscripciones { get; set; }
        public int TotalHombres { get; set; }
        public int TotalMujeres { get; set; }

        public async Task OnGetAsync()
        {
            await CargarCiclosAsync();

            if (!SelectedCicloId.HasValue || SelectedCicloId.Value <= 0)
            {
                SelectedCicloId = await ObtenerCicloPorDefectoAsync() ?? 0;
            }

            var cicloactual = await ObtenerCicloSeleccionadoAsync();
            if (cicloactual == null)
            {
                Inscripciones = new List<Inscripcion>();
                TotalInscripciones = 0;
                TotalHombres = 0;
                TotalMujeres = 0;
                return;
            }

            var query = _context.Inscripciones
                .AsNoTracking()
                .Include(i => i.Alumno)
                .ThenInclude(a => a.Carrera)
                .Include(i => i.Ciclo)
                .Where(i => i.CicloId == cicloactual.Id);

            Carreras = await _context.Carreras
                .AsNoTracking()
                .OrderBy(c => c.NombreCarrera)
                .Select(c => new SelectListItem { Value = c.CarreraId.ToString(), Text = c.NombreCarrera })
                .ToListAsync();

            if (SelectedCarreraId.HasValue && SelectedCarreraId.Value > 0)
            {
                query = query.Where(i => i.Alumno.CarreraId == SelectedCarreraId.Value);
            }

            // Filtro por género
            if (!string.IsNullOrEmpty(Genero))
            {
                if (int.TryParse(Genero, out int generoInt))
                {
                    query = query.Where(i => i.Alumno.Genero == generoInt);
                }
            }

            Inscripciones = await query.ToListAsync();

            var inscripcionesUnicas = Inscripciones
                .Where(i => i.Alumno != null)
                .GroupBy(i => i.AlumnoId)
                .Select(g => g.First())
                .ToList();

            TotalInscripciones = inscripcionesUnicas.Count;
            TotalHombres = inscripcionesUnicas.Count(i => i.Alumno!.Genero == 0);
            TotalMujeres = inscripcionesUnicas.Count(i => i.Alumno!.Genero == 1);
        }

        public async Task<IActionResult> OnGetGenerarReporteCompletoAsync()
        {
            try
            {
                var cicloSeleccionado = await ObtenerCicloSeleccionadoAsync();
                if (cicloSeleccionado == null)
                {
                    TempData["Error"] = "No se encontró el ciclo seleccionado para generar el reporte.";
                    return RedirectToPage(new { SelectedCicloId, SelectedCarreraId, Genero });
                }

                var pdfBytes = await _reporteService.GenerarReporteCompletoAsync(cicloSeleccionado.Id);
                var fileName = $"ReporteInscripcionesCompleto_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                Response.Headers["Content-Disposition"] = $"inline; filename={fileName}";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte: {ex.Message}";
                return RedirectToPage(new { SelectedCicloId, SelectedCarreraId, Genero });
            }
        }

        public async Task<IActionResult> OnGetGenerarReporteFiltradoAsync()
        {
            try
            {
                var cicloSeleccionado = await ObtenerCicloSeleccionadoAsync();
                if (cicloSeleccionado == null)
                {
                    TempData["Error"] = "No se encontró el ciclo seleccionado para generar el reporte.";
                    return RedirectToPage(new { SelectedCicloId, SelectedCarreraId, Genero });
                }

                var pdfBytes = await _reporteService.GenerarReporteFiltradoAsync(cicloSeleccionado.Id, SelectedCarreraId, Genero);
                var fileName = $"ReporteInscripcionesFiltrado_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                Response.Headers["Content-Disposition"] = $"inline; filename={fileName}";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte: {ex.Message}";
                return RedirectToPage(new { SelectedCicloId, SelectedCarreraId, Genero });
            }
        }

        public async Task<IActionResult> OnGetGenerarCuadroEstadisticoExcelAsync()
        {
            try
            {
                var cicloActual = await ObtenerCicloSeleccionadoAsync();

                if (cicloActual == null)
                {
                    TempData["Error"] = "No se encontró el ciclo seleccionado para generar el cuadro estadístico.";
                    return RedirectToPage(new { SelectedCicloId, SelectedCarreraId, Genero });
                }

                var archivo = await _reporteCuadroEstadisticoService.GenerarExcelAsync(cicloActual.Id, SelectedCarreraId);
                var fileName = $"Cuadro_Estadistico_{cicloActual.NCiclo}_{cicloActual.anio}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(
                    archivo,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar cuadro estadistico: {ex.Message}";
                return RedirectToPage(new { SelectedCicloId, SelectedCarreraId, Genero });
            }
        }

        public async Task<IActionResult> OnGetGenerarExcelInscripcionesAsync()
        {
            try
            {
                var cicloSeleccionado = await ObtenerCicloSeleccionadoAsync();
                if (cicloSeleccionado == null)
                {
                    TempData["Error"] = "No se encontró el ciclo seleccionado para generar el Excel.";
                    return RedirectToPage(new { SelectedCicloId, SelectedCarreraId, Genero });
                }

                var archivo = await _reporteInscripcionesExcelService.GenerarExcelAsync(
                    cicloSeleccionado.Id,
                    SelectedCarreraId,
                    Genero);

                var fileName = $"Inscripciones_{cicloSeleccionado.NCiclo}_{cicloSeleccionado.anio}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(
                    archivo,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar Excel de inscripciones: {ex.Message}";
                return RedirectToPage(new { SelectedCicloId, SelectedCarreraId, Genero });
            }
        }

        private async Task CargarCiclosAsync()
        {
            var ciclos = await _context.Ciclos
                .AsNoTracking()
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"Ciclo {c.NCiclo} - {c.anio}"
                })
                .ToListAsync();

            Ciclos = ciclos;
        }

        private async Task<int?> ObtenerCicloPorDefectoAsync()
        {
            var cicloActivoId = await _context.Ciclos
                .AsNoTracking()
                .Where(c => c.Activo)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            if (cicloActivoId.HasValue)
            {
                return cicloActivoId.Value;
            }

            return await _context.Ciclos
                .AsNoTracking()
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<Ciclo?> ObtenerCicloSeleccionadoAsync()
        {
            if (SelectedCicloId.HasValue && SelectedCicloId.Value > 0)
            {
                return await _context.Ciclos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == SelectedCicloId.Value);
            }

            var cicloPorDefectoId = await ObtenerCicloPorDefectoAsync();
            if (!cicloPorDefectoId.HasValue)
            {
                return null;
            }

            SelectedCicloId = cicloPorDefectoId.Value;

            return await _context.Ciclos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cicloPorDefectoId.Value);
        }
    }
} 
