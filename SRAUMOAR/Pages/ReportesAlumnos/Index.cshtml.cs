using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;

namespace SRAUMOAR.Pages.ReportesAlumnos
{
    [Authorize(Roles = "Administrador,Administracion,Docentes")]
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;
        private readonly ReporteAlumnosService _reporteService;

        public IndexModel(Contexto context, ReporteAlumnosService reporteService)
        {
            _context = context;
            _reporteService = reporteService;
        }

        public List<Carrera> Carreras { get; set; } = new List<Carrera>();

        public async Task OnGetAsync()
        {
            // Cargar las carreras para el filtro (opcional)
            Carreras = await _context.Carreras
                .Where(c => c.Activa)
                .OrderBy(c => c.NombreCarrera)
                .ToListAsync();
        }

        public async Task<IActionResult> OnGetExcelCompletoAsync()
        {
            try
            {
                var excelBytes = await _reporteService.GenerarReporteCompletoAsync();
                var fileName = $"Reporte_Alumnos_Completo_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnGetExcelFiltradoAsync(int? carreraId, int? estado, bool? ingresoPorEquivalencias, bool? inscritosEnCicloActivo)
        {
            try
            {
                var excelBytes = await _reporteService.GenerarReporteFiltradoAsync(carreraId, estado, ingresoPorEquivalencias, inscritosEnCicloActivo);
                var fileName = $"Reporte_Alumnos_Filtrado_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte filtrado: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}
