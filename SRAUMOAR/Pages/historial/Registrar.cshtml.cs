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
    public class RegistrarModel : PageModel
    {
        private readonly Contexto _context;

        public RegistrarModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty]
        public int AlumnoId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "El ciclo es requerido")]
        [Display(Name = "Ciclo")]
        public string CicloTexto { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "El pensúm es requerido")]
        public int PensumId { get; set; }

        [BindProperty]
        public int? CarreraId { get; set; }

        [BindProperty]
        public List<MateriaHistorialModel> Materias { get; set; } = new List<MateriaHistorialModel>();

        public string NombreAlumno { get; set; } = string.Empty;
        public SelectList PensumsList { get; set; } = null!;
        public SelectList CarrerasList { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int? alumnoId)
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

            // Cargar listas de pensúms y carreras
            await CargarListas();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CargarListas();
                return Page();
            }

            if (Materias == null || !Materias.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos una materia");
                await CargarListas();
                return Page();
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Si es primer registro (CarreraId es null), usar la carrera del pensum seleccionado
                int carreraIdParaHistorial;
                if (!CarreraId.HasValue || CarreraId.Value == 0)
                {
                    var pensum = await _context.Pensums
                        .Where(p => p.PensumId == PensumId)
                        .Select(p => p.CarreraId)
                        .FirstOrDefaultAsync();
                    
                    if (pensum != 0)
                    {
                        carreraIdParaHistorial = pensum;
                    }
                    else
                    {
                        throw new InvalidOperationException("No se pudo determinar la carrera del pensum seleccionado.");
                    }
                }
                else
                {
                    carreraIdParaHistorial = CarreraId.Value;
                }

                // Verificar si ya existe un historial académico para este alumno y carrera
                var historialAcademico = await _context.HistorialAcademico
                    .FirstOrDefaultAsync(h => h.AlumnoId == AlumnoId && h.CarreraId == carreraIdParaHistorial);

                if (historialAcademico == null)
                {
                    // Crear nuevo historial académico
                    historialAcademico = new HistorialAcademico
                    {
                        AlumnoId = AlumnoId,
                        CarreraId = carreraIdParaHistorial,
                        FechaRegistro = DateTime.Now
                    };

                    _context.HistorialAcademico.Add(historialAcademico);
                    await _context.SaveChangesAsync();
                }

                // Crear nuevo historial de ciclo
                var historialCiclo = new HistorialCiclo
                {
                    HistorialAcademicoId = historialAcademico.HistorialAcademicoId,
                    CicloTexto = CicloTexto,
                    PensumId = PensumId,
                    FechaRegistro = DateTime.Now
                };

                _context.HistorialCiclo.Add(historialCiclo);
                await _context.SaveChangesAsync();

                // Crear historial de materias
                foreach (var materia in Materias)
                {
                    var historialMateria = new HistorialMateria
                    {
                        HistorialCicloId = historialCiclo.HistorialCicloId,
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
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Historial académico registrado exitosamente.";
                return RedirectToPage("/historial/Ver", new { alumnoId = AlumnoId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar el historial: " + ex.Message);
                await CargarListas();
                return Page();
            }
        }

        private async Task CargarListas()
        {
            // Cargar carreras (siempre disponibles)
            var carreras = await _context.Carreras
                .OrderBy(c => c.NombreCarrera)
                .Select(c => new
                {
                    c.CarreraId,
                    c.NombreCarrera
                })
                .ToListAsync();

            CarrerasList = new SelectList(carreras, "CarreraId", "NombreCarrera");

            // Cargar pensúms
            var pensums = await _context.Pensums
                .OrderByDescending(p => p.Anio)
                .Select(p => new
                {
                    p.PensumId,
                    Nombre = $"Pensúm {p.Anio}"
                })
                .ToListAsync();

            PensumsList = new SelectList(pensums, "PensumId", "Nombre");
        }
    }


}
