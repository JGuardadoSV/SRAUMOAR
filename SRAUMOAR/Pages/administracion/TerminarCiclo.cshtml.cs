using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.administracion
{
    [IgnoreAntiforgeryToken]
    public class TerminarCicloModel : PageModel
    {
        private readonly Contexto _context;

        public TerminarCicloModel(Contexto context)
        {
            _context = context;
        }

        public Ciclo? CicloActual { get; set; }
        public List<MateriaSolventeViewModel> Materias { get; set; } = new List<MateriaSolventeViewModel>();
        public int TotalAlumnos { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public bool EsExito { get; set; }

        public class MateriaSolventeViewModel
        {
            public int MateriasGrupoId { get; set; }
            public int GrupoId { get; set; }
            public string NombreMateria { get; set; } = string.Empty;
            public string NombreGrupo { get; set; } = string.Empty;
            public string NombreCarrera { get; set; } = string.Empty;
            public bool Solvente { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostTerminarCicloAsync()
        {
            try
            {
                await CargarDatosAsync();

                if (CicloActual == null)
                {
                    TempData["Error"] = "No hay un ciclo activo";
                    return RedirectToPage();
                }

                // Verificar que todas las materias estén solventes
                var materiasNoSolventes = Materias.Where(m => !m.Solvente).ToList();
                if (materiasNoSolventes.Any())
                {
                    TempData["Error"] = $"No se puede terminar el ciclo. Hay {materiasNoSolventes.Count} materia(s) que no están marcadas como solventes.";
                    return RedirectToPage();
                }

                // Procesar historial de cada alumno
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var cicloTexto = $"{CicloActual.NCiclo:D2}-{CicloActual.anio}";

                    // Obtener todos los grupos del ciclo activo
                    var grupos = await _context.Grupo
                        .Include(g => g.Carrera)
                        .Include(g => g.MateriasGrupos)
                            .ThenInclude(mg => mg.MateriasInscritas)
                                .ThenInclude(mi => mi.Alumno)
                        .Include(g => g.MateriasGrupos)
                            .ThenInclude(mg => mg.MateriasInscritas)
                                .ThenInclude(mi => mi.Notas)
                                    .ThenInclude(n => n.ActividadAcademica)
                        .Include(g => g.MateriasGrupos)
                            .ThenInclude(mg => mg.Materia)
                        .Where(g => g.CicloId == CicloActual.Id)
                        .ToListAsync();

                    // Obtener actividades académicas del ciclo
                    var actividadesAcademicas = await _context.ActividadesAcademicas
                        .Where(a => a.CicloId == CicloActual.Id)
                        .OrderBy(a => a.TipoActividad)
                        .ThenBy(a => a.Fecha)
                        .ToListAsync();

                    // Obtener todos los alumnos únicos del ciclo
                    var alumnosIds = grupos
                        .SelectMany(g => g.MateriasGrupos)
                        .SelectMany(mg => mg.MateriasInscritas)
                        .Select(mi => mi.AlumnoId)
                        .Distinct()
                        .ToList();

                    int alumnosProcesados = 0;
                    int materiasProcesadas = 0;

                    foreach (var alumnoId in alumnosIds)
                    {
                        var alumno = await _context.Alumno
                            .Include(a => a.Carrera)
                            .FirstOrDefaultAsync(a => a.AlumnoId == alumnoId);

                        if (alumno == null) continue;

                        // Obtener todas las materias inscritas del alumno en este ciclo
                        var materiasInscritasAlumno = await _context.MateriasInscritas
                            .Include(mi => mi.MateriasGrupo)
                                .ThenInclude(mg => mg.Grupo)
                            .Include(mi => mi.MateriasGrupo)
                                .ThenInclude(mg => mg.Materia)
                                    .ThenInclude(m => m.Pensum)
                            .Include(mi => mi.Notas)
                                .ThenInclude(n => n.ActividadAcademica)
                            .Where(mi => mi.AlumnoId == alumnoId && 
                                        mi.MateriasGrupo.Grupo.CicloId == CicloActual.Id)
                            .ToListAsync();

                        if (!materiasInscritasAlumno.Any()) continue;

                        // Agrupar por carrera para crear historiales separados
                        var materiasPorCarrera = materiasInscritasAlumno
                            .Where(mi => mi.MateriasGrupo?.Materia?.Pensum != null && 
                                        mi.MateriasGrupo.Materia.Pensum.CarreraId > 0)
                            .GroupBy(mi => mi.MateriasGrupo.Materia.Pensum.CarreraId)
                            .ToList();

                        foreach (var grupoCarrera in materiasPorCarrera)
                        {
                            var carreraId = grupoCarrera.Key;
                            var materiasCarrera = grupoCarrera.ToList();
                            
                            if (!materiasCarrera.Any()) continue;

                            // Verificar o crear HistorialAcademico
                            var historialAcademico = await _context.HistorialAcademico
                                .FirstOrDefaultAsync(h => h.AlumnoId == alumnoId && h.CarreraId == carreraId);

                            if (historialAcademico == null)
                            {
                                historialAcademico = new HistorialAcademico
                                {
                                    AlumnoId = alumnoId,
                                    CarreraId = carreraId,
                                    FechaRegistro = DateTime.Now
                                };
                                _context.HistorialAcademico.Add(historialAcademico);
                                await _context.SaveChangesAsync();
                            }

                            // Obtener el pensum de la primera materia (todas deberían tener el mismo pensum en la misma carrera)
                            var pensumId = materiasCarrera.First().MateriasGrupo.Materia.PensumId;

                            // Verificar o crear HistorialCiclo
                            var historialCiclo = await _context.HistorialCiclo
                                .FirstOrDefaultAsync(hc => hc.HistorialAcademicoId == historialAcademico.HistorialAcademicoId &&
                                                          hc.CicloTexto == cicloTexto &&
                                                          hc.PensumId == pensumId);

                            if (historialCiclo == null)
                            {
                                historialCiclo = new HistorialCiclo
                                {
                                    HistorialAcademicoId = historialAcademico.HistorialAcademicoId,
                                    CicloTexto = cicloTexto,
                                    PensumId = pensumId,
                                    FechaRegistro = DateTime.Now
                                };
                                _context.HistorialCiclo.Add(historialCiclo);
                                await _context.SaveChangesAsync();
                            }

                            // Procesar cada materia inscrita
                            foreach (var materiaInscrita in materiasCarrera)
                            {
                                var materiaId = materiaInscrita.MateriasGrupo.MateriaId;

                                // Verificar si ya existe historial para esta materia en este ciclo
                                var historialMateriaExistente = await _context.HistorialMateria
                                    .FirstOrDefaultAsync(hm => hm.HistorialCicloId == historialCiclo.HistorialCicloId &&
                                                              hm.MateriaId == materiaId);

                                // Obtener notas y separar por tipo, ordenadas por fecha
                                var notasFiltradas = materiaInscrita.Notas
                                    ?.Where(n => actividadesAcademicas.Any(a => a.ActividadAcademicaId == n.ActividadAcademicaId))
                                    .ToList() ?? new List<Notas>();

                                // Separar por tipo y ordenar por fecha dentro de cada tipo
                                var laboratorios = notasFiltradas
                                    .Where(n => n.ActividadAcademica.TipoActividad == 1)
                                    .OrderBy(n => n.ActividadAcademica.Fecha)
                                    .Take(3)
                                    .ToList();
                                var parciales = notasFiltradas
                                    .Where(n => n.ActividadAcademica.TipoActividad == 2)
                                    .OrderBy(n => n.ActividadAcademica.Fecha)
                                    .Take(3)
                                    .ToList();

                                // Mapear notas alternando: LAB1→Nota1, PAR1→Nota2, LAB2→Nota3, PAR2→Nota4, LAB3→Nota5, PAR3→Nota6
                                decimal nota1 = 0, nota2 = 0, nota3 = 0, nota4 = 0, nota5 = 0, nota6 = 0;
                                
                                if (laboratorios.Count > 0) nota1 = laboratorios[0].Nota;
                                if (parciales.Count > 0) nota2 = parciales[0].Nota;
                                if (laboratorios.Count > 1) nota3 = laboratorios[1].Nota;
                                if (parciales.Count > 1) nota4 = parciales[1].Nota;
                                if (laboratorios.Count > 2) nota5 = laboratorios[2].Nota;
                                if (parciales.Count > 2) nota6 = parciales[2].Nota;

                                // Calcular promedio final considerando recuperación
                                // Si tiene reposición con 7 o más, se toma como 7 si o si y esa sería la nota promedio
                                decimal promedioFinal;
                                if (materiaInscrita.NotaRecuperacion.HasValue)
                                {
                                    if (materiaInscrita.NotaRecuperacion.Value >= 7)
                                    {
                                        promedioFinal = 7; // Si reposición >= 7, siempre se toma como 7
                                    }
                                    else
                                    {
                                        // Si reposición < 7, se usa la nota de reposición como promedio
                                        promedioFinal = materiaInscrita.NotaRecuperacion.Value;
                                    }
                                }
                                else
                                {
                                    // Si no tiene reposición, usar el promedio calculado de las actividades académicas
                                    promedioFinal = materiaInscrita.NotaPromedio > 0 ? materiaInscrita.NotaPromedio : 0;
                                }

                                bool aprobada = promedioFinal >= 7;

                                if (historialMateriaExistente != null)
                                {
                                    // Actualizar historial existente
                                    historialMateriaExistente.Nota1 = nota1;
                                    historialMateriaExistente.Nota2 = nota2;
                                    historialMateriaExistente.Nota3 = nota3;
                                    historialMateriaExistente.Nota4 = nota4;
                                    historialMateriaExistente.Nota5 = nota5;
                                    historialMateriaExistente.Nota6 = nota6;
                                    historialMateriaExistente.Promedio = promedioFinal;
                                    historialMateriaExistente.Aprobada = aprobada;
                                    historialMateriaExistente.FechaRegistro = DateTime.Now;
                                }
                                else
                                {
                                    // Crear nuevo historial
                                    var historialMateria = new HistorialMateria
                                    {
                                        HistorialCicloId = historialCiclo.HistorialCicloId,
                                        MateriaId = materiaId,
                                        Nota1 = nota1,
                                        Nota2 = nota2,
                                        Nota3 = nota3,
                                        Nota4 = nota4,
                                        Nota5 = nota5,
                                        Nota6 = nota6,
                                        Promedio = promedioFinal,
                                        Aprobada = aprobada,
                                        Equivalencia = false,
                                        ExamenSuficiencia = false,
                                        FechaRegistro = DateTime.Now
                                    };
                                    _context.HistorialMateria.Add(historialMateria);
                                }

                                materiasProcesadas++;
                            }
                        }

                        alumnosProcesados++;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["Success"] = $"Ciclo terminado exitosamente. Se procesaron {alumnosProcesados} alumno(s) y {materiasProcesadas} materia(s).";
                    return RedirectToPage();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["Error"] = $"Error al procesar el historial: {ex.Message}";
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostMarcarSolventeAsync()
        {
            try
            {
                // Leer los parámetros del Request.Form
                if (!int.TryParse(Request.Form["materiasGrupoId"].ToString(), out int materiasGrupoId))
                {
                    return new JsonResult(new { success = false, message = "Parámetro materiasGrupoId inválido." });
                }

                if (!bool.TryParse(Request.Form["solvente"].ToString(), out bool solvente))
                {
                    return new JsonResult(new { success = false, message = "Parámetro solvente inválido." });
                }

                var materiaGrupo = await _context.MateriasGrupo.FindAsync(materiasGrupoId);
                if (materiaGrupo == null)
                {
                    return new JsonResult(new { success = false, message = "No se encontró la materia del grupo." });
                }

                materiaGrupo.Solvente = solvente;
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Error al actualizar el estado de solvencia: " + ex.Message });
            }
        }

        private async Task CargarDatosAsync()
        {
            CicloActual = await _context.Ciclos
                .Where(c => c.Activo)
                .FirstOrDefaultAsync();

            if (CicloActual == null)
            {
                Mensaje = "No hay un ciclo activo";
                return;
            }

            // Obtener todas las materias del ciclo con su estado de solvencia
            var materiasGrupo = await _context.MateriasGrupo
                .Include(mg => mg.Materia)
                    .ThenInclude(m => m.Pensum)
                        .ThenInclude(p => p.Carrera)
                .Include(mg => mg.Grupo)
                .Where(mg => mg.Grupo.CicloId == CicloActual.Id)
                .ToListAsync();

            Materias = materiasGrupo.Select(mg => new MateriaSolventeViewModel
            {
                MateriasGrupoId = mg.MateriasGrupoId,
                GrupoId = mg.GrupoId,
                NombreMateria = mg.Materia?.NombreMateria ?? "Sin nombre",
                NombreGrupo = mg.Grupo?.Nombre ?? "Sin grupo",
                NombreCarrera = mg.Materia?.Pensum?.Carrera?.NombreCarrera ?? "Sin carrera",
                Solvente = mg.Solvente
            }).ToList();

            // Contar alumnos únicos del ciclo
            TotalAlumnos = await _context.MateriasInscritas
                .Include(mi => mi.MateriasGrupo)
                    .ThenInclude(mg => mg.Grupo)
                .Where(mi => mi.MateriasGrupo.Grupo.CicloId == CicloActual.Id)
                .Select(mi => mi.AlumnoId)
                .Distinct()
                .CountAsync();
        }
    }
}

