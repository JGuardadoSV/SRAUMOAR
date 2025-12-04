using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.historial
{
    public class VerModel : PageModel
    {
        private readonly Contexto _context;

        public VerModel(Contexto context)
        {
            _context = context;
        }

        public int AlumnoId { get; set; }
        public string NombreAlumno { get; set; } = string.Empty;
        public List<HistorialCiclo> HistorialCiclos { get; set; } = new List<HistorialCiclo>();
        public int TotalMaterias { get; set; }
        public decimal TotalUV { get; set; }
        public List<Carrera> CarrerasDisponibles { get; set; } = new List<Carrera>();
        public int? CarreraSeleccionadaId { get; set; }

        public async Task<IActionResult> OnGetAsync(int? alumnoId, int? carreraId = null)
        {
            if (alumnoId == null)
            {
                return RedirectToPage("/historial/Index");
            }

            AlumnoId = alumnoId.Value;

            // Obtener información del alumno
            var alumno = await _context.Alumno
                .FirstOrDefaultAsync(a => a.AlumnoId == AlumnoId);

            if (alumno == null)
            {
                return NotFound();
            }

            NombreAlumno = $"{alumno.Apellidos}, {alumno.Nombres}";

            // Obtener historial académico del alumno
            var historialAcademico = await _context.HistorialAcademico
                .Include(h => h.CiclosHistorial)
                    .ThenInclude(hc => hc.Pensum)
                .Include(h => h.CiclosHistorial)
                    .ThenInclude(hc => hc.MateriasHistorial)
                        .ThenInclude(hm => hm.Materia)
                .Include(h => h.Carrera)
                .Where(h => h.AlumnoId == AlumnoId)
                .ToListAsync();

            if (historialAcademico != null && historialAcademico.Any())
            {
                // Obtener carreras disponibles
                CarrerasDisponibles = historialAcademico
                    .Where(h => h.Carrera != null)
                    .Select(h => h.Carrera)
                    .Distinct()
                    .ToList();

                // Si no se especifica carrera, usar la primera disponible
                if (carreraId == null && CarrerasDisponibles.Any())
                {
                    carreraId = CarrerasDisponibles.First().CarreraId;
                }

                // Filtrar por carrera específica
                if (carreraId.HasValue)
                {
                    CarreraSeleccionadaId = carreraId.Value;
                    var historialPorCarrera = historialAcademico
                        .Where(h => h.CarreraId == carreraId.Value)
                        .FirstOrDefault();

                    if (historialPorCarrera != null)
                    {
                        HistorialCiclos = historialPorCarrera.CiclosHistorial?.ToList() ?? new List<HistorialCiclo>();
                    }
                }
                else
                {
                    // Si no hay carrera seleccionada, mostrar todos los ciclos de todos los historiales
                    HistorialCiclos = historialAcademico
                        .SelectMany(h => h.CiclosHistorial ?? new List<HistorialCiclo>())
                        .ToList();
                }
                
                // Calcular totales considerando materias libres
                TotalMaterias = HistorialCiclos.Sum(hc => hc.MateriasHistorial?.Count ?? 0);
                TotalUV = HistorialCiclos.Sum(hc => hc.MateriasHistorial?.Sum(hm => 
                    hm.Materia != null ? hm.Materia.uv : (hm.MateriaUnidadesValorativasLibre ?? 0)) ?? 0);
            }

            return Page();
        }
    }
}
