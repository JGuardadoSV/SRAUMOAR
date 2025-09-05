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
            NombrePensum = $"Pensúm {historialCiclo.Pensum.Anio}";
            PensumId = historialCiclo.PensumId;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (Materias == null || !Materias.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos una materia");
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
                    var historialMateria = new HistorialMateria
                    {
                        HistorialCicloId = HistorialCicloId,
                        MateriaId = materia.MateriaId,
                        Nota1 = materia.Nota1,
                        Nota2 = materia.Nota2,
                        Nota3 = materia.Nota3,
                        Nota4 = materia.Nota4,
                        Nota5 = materia.Nota5,
                        Nota6 = materia.Nota6,
                        Promedio = materia.Promedio,
                        Aprobada = materia.Aprobada,
                        Equivalencia = materia.Equivalencia,
                        FechaRegistro = DateTime.Now
                    };

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
                return Page();
            }
        }
    }


}
