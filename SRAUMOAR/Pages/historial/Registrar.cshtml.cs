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
        public int PensumId { get; set; }

        [BindProperty]
        public int? CarreraId { get; set; }

        [BindProperty]
        public bool UsarPensumExistente { get; set; } = true;

        [BindProperty]
        [Display(Name = "Nombre del pensúm")]
        public string? PensumNombreLibre { get; set; }

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
            // Validación condicional según modo
            if (UsarPensumExistente)
            {
                if (PensumId == 0)
                {
                    ModelState.AddModelError(nameof(PensumId), "Debe seleccionar un pensúm.");
                }

                if (Materias == null || !Materias.Any())
                {
                    ModelState.AddModelError("", "Debe agregar al menos una materia");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(PensumNombreLibre))
                {
                    ModelState.AddModelError(nameof(PensumNombreLibre), "Debe ingresar el nombre del pensúm.");
                }

                if (!CarreraId.HasValue || CarreraId.Value == 0)
                {
                    ModelState.AddModelError(nameof(CarreraId), "Debe seleccionar la carrera para el historial.");
                }
            }

            if (!ModelState.IsValid)
            {
                await CargarListas();
                return Page();
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Calcular carreraIdParaHistorial según el modo
                int carreraIdParaHistorial;

                if (UsarPensumExistente)
                {
                    int carreraDesdePensum = await _context.Pensums
                        .Where(p => p.PensumId == PensumId)
                        .Select(p => p.CarreraId)
                        .FirstOrDefaultAsync();

                    if (carreraDesdePensum == 0)
                    {
                        throw new InvalidOperationException("No se pudo determinar la carrera del pensúm seleccionado.");
                    }

                    carreraIdParaHistorial = CarreraId ?? carreraDesdePensum;
                }
                else
                {
                    // En modo manual usamos la carrera seleccionada explícitamente
                    if (!CarreraId.HasValue || CarreraId.Value == 0)
                    {
                        throw new InvalidOperationException("Debe seleccionar la carrera para el historial.");
                    }
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
                    PensumId = UsarPensumExistente ? PensumId : (int?)null,
                    PensumNombreLibre = UsarPensumExistente ? null : PensumNombreLibre,
                    FechaRegistro = DateTime.Now
                };

                _context.HistorialCiclo.Add(historialCiclo);
                await _context.SaveChangesAsync();

                // Crear historial de materias (tanto del pensum como manuales)
                if (Materias != null && Materias.Any())
                {
                    foreach (var materia in Materias)
                    {
                        // Determinar si esta materia es manual (tiene campos libres) o del pensum
                        bool esMateriaManual = !string.IsNullOrEmpty(materia.MateriaCodigoLibre) 
                            || !string.IsNullOrEmpty(materia.MateriaNombreLibre) 
                            || materia.MateriaUnidadesValorativasLibre.HasValue;

                        var historialMateria = new HistorialMateria
                        {
                            HistorialCicloId = historialCiclo.HistorialCicloId,
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
                }

                await transaction.CommitAsync();

                TempData["SuccessMessage"] = UsarPensumExistente
                    ? "Historial académico registrado exitosamente."
                    : "Historial académico registrado. Puede agregar las materias manualmente.";

                // Redirigir a Ver con el carreraId para que muestre el historial correcto
                return RedirectToPage("/historial/Ver", new { alumnoId = AlumnoId, carreraId = carreraIdParaHistorial });
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
