using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Pages.administracion
{
    [Authorize(Roles = "Administrador,Administracion")]
    public class EditarNotasGrupoModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;

        public EditarNotasGrupoModel(SRAUMOAR.Modelos.Contexto context)
        {
            _context = context;
        }

        public int GrupoId { get; set; }
        public string NombreGrupo { get; set; } = string.Empty;
        public string NombreCarrera { get; set; } = string.Empty;
        public string CicloActual { get; set; } = string.Empty;
        public IList<EstudianteConNotas> EstudiantesConNotas { get; set; } = default!;
        public IList<MateriaDelGrupo> MateriasDelGrupo { get; set; } = default!;
        public IList<ActividadAcademica> ActividadesAcademicas { get; set; } = default!;

        [BindProperty]
        public Dictionary<int, Dictionary<int, Dictionary<int, NotaEditar>>> NotasEditar { get; set; } = new();

        public class EstudianteConNotas
        {
            public int AlumnoId { get; set; }
            public string NombreCompleto { get; set; } = string.Empty;
            public string CodigoEstudiante { get; set; } = string.Empty;
            public int MateriasInscritasId { get; set; }
            public IList<Notas> Notas { get; set; } = new List<Notas>();

            public decimal CalcularPromedioMateria(int materiasGrupoId)
            {
                var notasMateria = Notas.Where(n => n.MateriasInscritas.MateriasGrupoId == materiasGrupoId).ToList();
                if (!notasMateria.Any()) return 0;

                decimal sumaPonderada = 0;
                decimal totalPorcentaje = 0;

                foreach (var nota in notasMateria)
                {
                    sumaPonderada += nota.Nota * (nota.ActividadAcademica?.Porcentaje ?? 0);
                    totalPorcentaje += nota.ActividadAcademica?.Porcentaje ?? 0;
                }

                return totalPorcentaje > 0 ? Math.Round(sumaPonderada / totalPorcentaje, 2) : 0;
            }
        }

        public class MateriaDelGrupo
        {
            public int MateriasGrupoId { get; set; }
            public string NombreMateria { get; set; } = string.Empty;
            public string CodigoMateria { get; set; } = string.Empty;
        }

        public class NotaEditar
        {
            public int NotasId { get; set; }
            public int MateriasInscritasId { get; set; }
            public int ActividadAcademicaId { get; set; }
            [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
            public decimal Nota { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int grupoId)
        {
            GrupoId = grupoId;

            var grupo = await _context.Grupo
                .Include(g => g.Carrera)
                .Include(g => g.Ciclo)
                .FirstOrDefaultAsync(g => g.GrupoId == grupoId);

            if (grupo == null)
            {
                return NotFound();
            }

            NombreGrupo = grupo.Nombre;
            NombreCarrera = grupo.Carrera.NombreCarrera;
            CicloActual = $"Ciclo {grupo.Ciclo.NCiclo} - {grupo.Ciclo.anio}";

            // Obtener materias del grupo
            MateriasDelGrupo = await _context.MateriasGrupo
                .Include(mg => mg.Materia)
                .Where(mg => mg.GrupoId == grupoId)
                .Select(mg => new MateriaDelGrupo
                {
                    MateriasGrupoId = mg.MateriasGrupoId,
                    NombreMateria = mg.Materia.NombreMateria,
                    CodigoMateria = mg.Materia.CodigoMateria
                })
                .ToListAsync();

            // Obtener actividades académicas del ciclo
            ActividadesAcademicas = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == grupo.CicloId)
                .OrderBy(a => a.Nombre)
                .ToListAsync();

            // Obtener estudiantes con sus notas
            var materiasInscritas = await _context.MateriasInscritas
                .Include(mi => mi.Alumno)
                .Include(mi => mi.MateriasGrupo)
                .Include(mi => mi.Notas)
                    .ThenInclude(n => n.ActividadAcademica)
                .Where(mi => mi.MateriasGrupo.GrupoId == grupoId)
                .ToListAsync();

            EstudiantesConNotas = materiasInscritas
                .GroupBy(mi => mi.AlumnoId)
                .Select(g => new EstudianteConNotas
                {
                    AlumnoId = g.Key,
                    NombreCompleto = g.First().Alumno.Nombres + " " + g.First().Alumno.Apellidos,
                    CodigoEstudiante = g.First().Alumno.Email?.Split('@')[0] ?? "",
                    MateriasInscritasId = g.First().MateriasInscritasId,
                    Notas = g.SelectMany(mi => mi.Notas).ToList()
                })
                .OrderBy(e => e.NombreCompleto)
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int grupoId)
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync(grupoId);
                return Page();
            }

            try
            {
                foreach (var alumnoNotas in NotasEditar.Values)
                {
                    foreach (var materiaNotas in alumnoNotas.Values)
                    {
                        foreach (var notaData in materiaNotas.Values)
                        {
                            if (notaData.NotasId > 0)
                            {
                                // Actualizar nota existente
                                var notaExistente = await _context.Notas.FindAsync(notaData.NotasId);
                                if (notaExistente != null)
                                {
                                    notaExistente.Nota = notaData.Nota;
                                }
                            }
                            else if (notaData.Nota > 0)
                            {
                                // Crear nueva nota
                                var nuevaNota = new Notas
                                {
                                    Nota = notaData.Nota,
                                    MateriasInscritasId = notaData.MateriasInscritasId,
                                    ActividadAcademicaId = notaData.ActividadAcademicaId,
                                    FechaRegistro = DateTime.Now
                                };
                                _context.Notas.Add(nuevaNota);
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Las notas se han actualizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al actualizar las notas: " + ex.Message;
            }

            return RedirectToPage("./EditarNotasGrupo", new { grupoId });
        }
    }
}
