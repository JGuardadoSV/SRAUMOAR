using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Materias;
using SRAUMOAR.Modelos;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Pages.historial
{
    public class AgregarMateriaModel : PageModel
    {
        private readonly Contexto _context;

        public AgregarMateriaModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public int HistorialCicloId { get; set; }

        [BindProperty]
        public List<MateriaHistorialModel> Materias { get; set; } = new List<MateriaHistorialModel>();

        // Datos pre-cargados del historial existente
        public int AlumnoId { get; set; }
        public string NombreAlumno { get; set; } = string.Empty;
        public string CicloTexto { get; set; } = string.Empty;
        public string NombreCarrera { get; set; } = string.Empty;
        public string NombrePensum { get; set; } = string.Empty;
        public int PensumId { get; set; }
        public bool EsModoManual { get; set; }

        public async Task<IActionResult> OnGetAsync(int? historialCicloId)
        {
            if (historialCicloId == null)
            {
                return RedirectToPage("/historial/Index");
            }

            HistorialCicloId = historialCicloId.Value;

            // Obtener información del historial del ciclo
            var historialCiclo = await _context.HistorialCiclo
                .Include(hc => hc.HistorialAcademico)
                    .ThenInclude(ha => ha.Alumno)
                .Include(hc => hc.HistorialAcademico)
                    .ThenInclude(ha => ha.Carrera)
                .Include(hc => hc.Pensum)
                .Include(hc => hc.MateriasHistorial)
                    .ThenInclude(hm => hm.Materia)
                .FirstOrDefaultAsync(hc => hc.HistorialCicloId == HistorialCicloId);

            if (historialCiclo == null)
            {
                return NotFound();
            }

            // Cargar datos pre-cargados
            AlumnoId = historialCiclo.HistorialAcademico.Alumno.AlumnoId;
            NombreAlumno = $"{historialCiclo.HistorialAcademico.Alumno.Apellidos}, {historialCiclo.HistorialAcademico.Alumno.Nombres}";
            CicloTexto = historialCiclo.CicloTexto;
            NombreCarrera = historialCiclo.HistorialAcademico.Carrera?.NombreCarrera ?? "Sin carrera";
            
            // Detectar modo manual: PensumId es null O tiene PensumNombreLibre
            EsModoManual = historialCiclo.PensumId == null || !string.IsNullOrEmpty(historialCiclo.PensumNombreLibre);
            
            if (EsModoManual)
            {
                NombrePensum = historialCiclo.PensumNombreLibre ?? "Pensúm manual";
                PensumId = 0;
            }
            else
            {
                NombrePensum = $"Pensúm {historialCiclo.Pensum?.Anio ?? 0}";
                PensumId = historialCiclo.PensumId ?? 0;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Recargar datos del historial para obtener EsModoManual
            var historialCicloTemp = await _context.HistorialCiclo
                .FirstOrDefaultAsync(hc => hc.HistorialCicloId == HistorialCicloId);

            if (historialCicloTemp == null)
            {
                return NotFound();
            }

            // Detectar modo manual: PensumId es null O tiene PensumNombreLibre
            EsModoManual = historialCicloTemp.PensumId == null || !string.IsNullOrEmpty(historialCicloTemp.PensumNombreLibre);

            if (!ModelState.IsValid)
            {
                // Recargar datos para mostrar la página
                await OnGetAsync(HistorialCicloId);
                return Page();
            }

            if (Materias == null || !Materias.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos una materia");
                await OnGetAsync(HistorialCicloId);
                return Page();
            }

            try
            {
                // Verificar que el historial del ciclo existe
                var historialCiclo = await _context.HistorialCiclo
                    .FirstOrDefaultAsync(hc => hc.HistorialCicloId == HistorialCicloId);

                if (historialCiclo == null)
                {
                    return NotFound();
                }

                // Crear historial de materias
                foreach (var materia in Materias)
                {
                    // Determinar si esta materia es manual (tiene campos libres) o del pensum
                    bool esMateriaManual = !string.IsNullOrEmpty(materia.MateriaCodigoLibre) 
                        || !string.IsNullOrEmpty(materia.MateriaNombreLibre) 
                        || materia.MateriaUnidadesValorativasLibre.HasValue;

                    var historialMateria = new HistorialMateria
                    {
                        HistorialCicloId = HistorialCicloId,
                        Nota1 = materia.Nota1,
                        Nota2 = materia.Nota2,
                        Nota3 = materia.Nota3,
                        Nota4 = materia.Nota4,
                        Nota5 = materia.Nota5,
                        Nota6 = materia.Nota6,
                        Promedio = materia.Promedio,
                        Aprobada = materia.Aprobada,
                        Equivalencia = materia.Equivalencia,
                        ExamenSuficiencia = materia.ExamenSuficiencia,
                        FechaRegistro = DateTime.Now
                    };

                    if (esMateriaManual)
                    {
                        // Materia manual: usar campos libres
                        historialMateria.MateriaId = null;
                        historialMateria.MateriaCodigoLibre = materia.MateriaCodigoLibre;
                        historialMateria.MateriaNombreLibre = materia.MateriaNombreLibre;
                        historialMateria.MateriaUnidadesValorativasLibre = materia.MateriaUnidadesValorativasLibre;
                    }
                    else
                    {
                        // Materia del pensum: usar MateriaId
                        historialMateria.MateriaId = materia.MateriaId;
                    }

                    _context.HistorialMateria.Add(historialMateria);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Materias agregadas exitosamente al historial.";
                
                // Redirigir de vuelta a Ver el historial del alumno
                var historialAcademico = await _context.HistorialCiclo
                    .Where(hc => hc.HistorialCicloId == HistorialCicloId)
                    .Select(hc => hc.HistorialAcademico.AlumnoId)
                    .FirstOrDefaultAsync();

                return RedirectToPage("/historial/Ver", new { alumnoId = historialAcademico });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar las materias: " + ex.Message);
                await OnGetAsync(HistorialCicloId);
                return Page();
            }
        }
    }


}
