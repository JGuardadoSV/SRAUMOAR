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
        public List<HistorialCiclo> TodosLosCiclos { get; set; } = new List<HistorialCiclo>(); // Todos los ciclos del alumno para mover materias
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
                
                // Obtener todos los ciclos del alumno (sin filtrar por carrera) para poder mover materias
                TodosLosCiclos = historialAcademico
                    .SelectMany(h => h.CiclosHistorial ?? new List<HistorialCiclo>())
                    .OrderByDescending(c => c.CicloTexto)
                    .ToList();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostActualizarNotasAsync(
            int historialMateriaId,
            int historialCicloIdActual,
            decimal nota1,
            decimal nota2,
            decimal nota3,
            decimal nota4,
            decimal nota5,
            decimal nota6,
            decimal promedio,
            bool aprobada,
            bool equivalencia,
            bool examenSuficiencia,
            int? nuevoCicloId = null,
            string? nuevoCicloTexto = null,
            string? nuevoPensumNombre = null)
        {
            try
            {
                var historialMateria = await _context.HistorialMateria
                    .Include(hm => hm.HistorialCiclo)
                        .ThenInclude(hc => hc.HistorialAcademico)
                    .FirstOrDefaultAsync(hm => hm.HistorialMateriaId == historialMateriaId);

                if (historialMateria == null)
                {
                    return new JsonResult(new { success = false, message = "No se encontró la materia en el historial." });
                }

                // Validar que las notas estén en el rango válido
                if (nota1 < 0 || nota1 > 10 || nota2 < 0 || nota2 > 10 || nota3 < 0 || nota3 > 10 ||
                    nota4 < 0 || nota4 > 10 || nota5 < 0 || nota5 > 10 || nota6 < 0 || nota6 > 10 ||
                    promedio < 0 || promedio > 10)
                {
                    return new JsonResult(new { success = false, message = "Las notas y el promedio deben estar entre 0 y 10." });
                }

                // Si se quiere mover a otro ciclo o crear uno nuevo
                int cicloIdFinal = historialCicloIdActual;
                
                if (!string.IsNullOrWhiteSpace(nuevoCicloTexto))
                {
                    // Crear un nuevo ciclo
                    var historialAcademico = historialMateria.HistorialCiclo?.HistorialAcademico;
                    if (historialAcademico == null)
                    {
                        return new JsonResult(new { success = false, message = "No se pudo determinar el historial académico." });
                    }

                    var nuevoCiclo = new HistorialCiclo
                    {
                        HistorialAcademicoId = historialAcademico.HistorialAcademicoId,
                        CicloTexto = nuevoCicloTexto.Trim(),
                        PensumNombreLibre = string.IsNullOrWhiteSpace(nuevoPensumNombre) ? null : nuevoPensumNombre.Trim(),
                        FechaRegistro = DateTime.Now
                    };

                    _context.HistorialCiclo.Add(nuevoCiclo);
                    await _context.SaveChangesAsync();
                    cicloIdFinal = nuevoCiclo.HistorialCicloId;
                }
                else if (nuevoCicloId.HasValue && nuevoCicloId.Value > 0)
                {
                    // Mover a un ciclo existente
                    var cicloExistente = await _context.HistorialCiclo
                        .FirstOrDefaultAsync(hc => hc.HistorialCicloId == nuevoCicloId.Value);
                    
                    if (cicloExistente == null)
                    {
                        return new JsonResult(new { success = false, message = "El ciclo seleccionado no existe." });
                    }
                    
                    cicloIdFinal = nuevoCicloId.Value;
                }

                // Actualizar las notas y el ciclo si cambió
                historialMateria.Nota1 = nota1;
                historialMateria.Nota2 = nota2;
                historialMateria.Nota3 = nota3;
                historialMateria.Nota4 = nota4;
                historialMateria.Nota5 = nota5;
                historialMateria.Nota6 = nota6;
                historialMateria.Promedio = promedio;
                historialMateria.Aprobada = aprobada;
                historialMateria.Equivalencia = equivalencia;
                historialMateria.ExamenSuficiencia = examenSuficiencia;
                historialMateria.FechaRegistro = DateTime.Now;
                
                // Si cambió el ciclo, actualizar
                if (cicloIdFinal != historialCicloIdActual)
                {
                    historialMateria.HistorialCicloId = cicloIdFinal;
                }

                _context.HistorialMateria.Update(historialMateria);
                await _context.SaveChangesAsync();

                var mensaje = cicloIdFinal != historialCicloIdActual 
                    ? "Notas actualizadas y materia movida exitosamente." 
                    : "Notas actualizadas exitosamente.";

                return new JsonResult(new { success = true, message = mensaje });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = $"Error al actualizar las notas: {ex.Message}" });
            }
        }
    }
}
