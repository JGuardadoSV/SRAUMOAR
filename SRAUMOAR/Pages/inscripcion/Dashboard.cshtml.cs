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
        
        public DashboardModel(SRAUMOAR.Modelos.Contexto context, ReporteInscripcionesService reporteService)
        {
            _context = context;
            _reporteService = reporteService;
        }

        public List<SelectListItem> Carreras { get; set; } = new List<SelectListItem>();
        public IList<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
        [BindProperty(SupportsGet = true)]
        public int? SelectedCarreraId { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Genero { get; set; }
        public int TotalInscripciones { get; set; }
        public int TotalHombres { get; set; }
        public int TotalMujeres { get; set; }

        public async Task OnGetAsync()
        {
            var cicloactual = await _context.Ciclos.Where(x => x.Activo == true).FirstOrDefaultAsync();
            var query = _context.Inscripciones
                .Include(i => i.Alumno)
                .ThenInclude(a => a.Carrera)
                .Include(i => i.Ciclo)
                .Where(i => i.CicloId == cicloactual.Id);

            // Filtro por carrera
            Carreras = await _context.Carreras
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
            TotalInscripciones = Inscripciones.Count;
            TotalHombres = Inscripciones.Count(i => i.Alumno.Genero == 0);
            TotalMujeres = Inscripciones.Count(i => i.Alumno.Genero == 1);
        }

        public async Task<IActionResult> OnGetGenerarReporteCompletoAsync()
        {
            try
            {
                var pdfBytes = await _reporteService.GenerarReporteCompletoAsync();
                var fileName = $"ReporteInscripcionesCompleto_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                Response.Headers["Content-Disposition"] = $"inline; filename={fileName}";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnGetGenerarReporteFiltradoAsync()
        {
            try
            {
                var pdfBytes = await _reporteService.GenerarReporteFiltradoAsync(SelectedCarreraId, Genero);
                var fileName = $"ReporteInscripcionesFiltrado_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                Response.Headers["Content-Disposition"] = $"inline; filename={fileName}";
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
} 