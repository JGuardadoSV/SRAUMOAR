using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;
using System.Security.Claims;

namespace SRAUMOAR.Pages.portal.estudiante
{
    [Authorize(Roles = "Estudiantes")]
    public class MiHistorialModel : PageModel
    {
        private readonly Contexto _context;

        public MiHistorialModel(Contexto context)
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

        public async Task<IActionResult> OnGetAsync(int? carreraId = null)
        {
            // Obtener el ID del usuario autenticado
            var userId = User.FindFirstValue("UserId") ?? "0";
            int idusuario = int.Parse(userId);

            // Obtener el alumno asociado al usuario
            var alumno = await _context.Alumno
                .Include(a => a.Carrera)
                .FirstOrDefaultAsync(a => a.UsuarioId == idusuario);

            if (alumno == null)
            {
                return NotFound();
            }

            AlumnoId = alumno.AlumnoId;
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

                // Si no se especifica carrera, usar la primera disponible o la del alumno
                if (carreraId == null)
                {
                    if (CarrerasDisponibles.Any())
                    {
                        carreraId = CarrerasDisponibles.First().CarreraId;
                    }
                    else if (alumno.CarreraId.HasValue)
                    {
                        carreraId = alumno.CarreraId.Value;
                    }
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
                        
                        // Calcular totales solo para esta carrera
                        TotalMaterias = HistorialCiclos.Sum(hc => hc.MateriasHistorial?.Count ?? 0);
                        TotalUV = HistorialCiclos.Sum(hc => hc.MateriasHistorial?.Sum(hm => hm.Materia?.uv ?? 0) ?? 0);
                    }
                }
            }

            return Page();
        }
    }
}
