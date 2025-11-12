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
    [IgnoreAntiforgeryToken]
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

        // Método estático compartido para calcular el promedio
        private static decimal CalcularPromedioMateriaComun(ICollection<Notas> notas, IList<ActividadAcademica> actividadesAcademicas)
        {
            if (actividadesAcademicas == null || !actividadesAcademicas.Any())
                return 0;

            decimal sumaPonderada = 0;
            decimal totalPorcentaje = 0;

            // Iterar sobre TODAS las actividades académicas del ciclo
            foreach (var actividad in actividadesAcademicas)
            {
                if (actividad == null) continue;

                int porcentaje = actividad.Porcentaje;
                totalPorcentaje += porcentaje;

                // Buscar si existe una nota registrada para esta actividad
                var notaRegistrada = notas
                    ?.FirstOrDefault(n => n.ActividadAcademicaId == actividad.ActividadAcademicaId);

                // Si existe nota registrada, usar su valor; si no, usar 0
                decimal valorNota = notaRegistrada?.Nota ?? 0;

                sumaPonderada += valorNota * porcentaje;
            }

            // Si no hay porcentajes, retornar 0
            if (totalPorcentaje <= 0) return 0;

            return Math.Round(sumaPonderada / totalPorcentaje, 2);
        }

        public class EstudianteConNotas
        {
            public int AlumnoId { get; set; }
            public string NombreCompleto { get; set; } = string.Empty;
            public string CodigoEstudiante { get; set; } = string.Empty;
            public int MateriasInscritasId { get; set; }
            public IList<Notas> Notas { get; set; } = new List<Notas>();
            public List<MateriasInscritas> MateriasInscritas { get; set; } = new List<MateriasInscritas>();

            public decimal CalcularPromedioMateria(int materiasGrupoId, IList<ActividadAcademica> actividadesAcademicas)
            {
                // Obtener las notas registradas para esta materia
                var notasMateria = Notas
                    .Where(n => n.MateriasInscritas != null && 
                               n.MateriasInscritas.MateriasGrupoId == materiasGrupoId)
                    .ToList();

                return CalcularPromedioMateriaComun(notasMateria, actividadesAcademicas);
            }
        }

        public class MateriaDelGrupo
        {
            public int MateriasGrupoId { get; set; }
            public string NombreMateria { get; set; } = string.Empty;
            public string CodigoMateria { get; set; } = string.Empty;
            public string NombreDocente { get; set; } = string.Empty;
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
                .Include(mg => mg.Docente)
                .Where(mg => mg.GrupoId == grupoId)
                .Select(mg => new MateriaDelGrupo
                {
                    MateriasGrupoId = mg.MateriasGrupoId,
                    NombreMateria = mg.Materia.NombreMateria,
                    CodigoMateria = mg.Materia.CodigoMateria,
                    NombreDocente = mg.Docente != null ? $"{mg.Docente.Nombres} {mg.Docente.Apellidos}" : "Sin asignar"
                })
                .ToListAsync();

            // Obtener actividades académicas del ciclo ordenadas por FechaInicio
            ActividadesAcademicas = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == grupo.CicloId)
                .OrderBy(a => a.FechaInicio)
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
                    // Formato: Apellidos Nombres
                    NombreCompleto = ($"{g.First().Alumno.Apellidos} {g.First().Alumno.Nombres}").Trim(),
                    CodigoEstudiante = g.First().Alumno.Email?.Split('@')[0] ?? "",
                    MateriasInscritasId = 0, // Se establecerá dinámicamente en la vista
                    Notas = g.SelectMany(mi => mi.Notas).ToList(),
                    MateriasInscritas = g.ToList() // Agregar todas las materias inscritas
                })
                .OrderBy(e => e.NombreCompleto)
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostGuardarNotasEstudianteAsync()
        {
            try
            {
                Console.WriteLine("=== INICIO GuardarNotasEstudiante ===");
                
                // Leer el cuerpo de la solicitud
                using var reader = new StreamReader(Request.Body);
                var requestBody = await reader.ReadToEndAsync();
                Console.WriteLine($"Request body: {requestBody}");
                
                if (string.IsNullOrEmpty(requestBody))
                {
                    Console.WriteLine("Request body está vacío");
                    return new JsonResult(new { success = false, message = "No se recibieron datos" });
                }
                
                // Deserializar JSON
                var request = System.Text.Json.JsonSerializer.Deserialize<GuardarNotasRequest>(requestBody);
                Console.WriteLine($"Request deserializado: {request != null}");
                
                if (request == null)
                {
                    Console.WriteLine("No se pudo deserializar el request");
                    return new JsonResult(new { success = false, message = "Error al procesar los datos" });
                }
                
                Console.WriteLine($"AlumnoId: {request.AlumnoId}");
                Console.WriteLine($"MateriasGrupoId: {request.MateriasGrupoId}");
                Console.WriteLine($"MateriasInscritasId: {request.MateriasInscritasId}");
                Console.WriteLine($"Número de notas recibidas: {request.Notas?.Count ?? 0}");

                if (request.Notas == null || !request.Notas.Any())
                {
                    return new JsonResult(new { success = false, message = "No hay notas para guardar" });
                }

                int notasActualizadas = 0;
                int notasCreadas = 0;
                
                // Log de todas las notas existentes para este MateriasInscritasId
                var notasExistentes = await _context.Notas
                    .Where(n => n.MateriasInscritasId == request.MateriasInscritasId)
                    .ToListAsync();
                
                Console.WriteLine($"=== NOTAS EXISTENTES EN BD ===");
                Console.WriteLine($"MateriasInscritasId: {request.MateriasInscritasId}");
                Console.WriteLine($"Total notas encontradas: {notasExistentes.Count}");
                foreach (var nota in notasExistentes)
                {
                    Console.WriteLine($"  - NotasId: {nota.NotasId}, ActividadAcademicaId: {nota.ActividadAcademicaId}, Nota: {nota.Nota}");
                }
                
                foreach (var notaData in request.Notas)
                {
                    Console.WriteLine($"=== PROCESANDO NOTA ===");
                    Console.WriteLine($"MateriasInscritasId: {request.MateriasInscritasId}");
                    Console.WriteLine($"ActividadAcademicaId: {notaData.ActividadAcademicaId}");
                    Console.WriteLine($"Nota a guardar: {notaData.Nota}");
                    
                    // Buscar nota existente
                    var notaExistente = await _context.Notas
                        .FirstOrDefaultAsync(n => n.MateriasInscritasId == request.MateriasInscritasId && 
                                                 n.ActividadAcademicaId == notaData.ActividadAcademicaId);

                    if (notaExistente != null)
                    {
                        // Actualizar nota existente
                        notaExistente.Nota = notaData.Nota;
                        notaExistente.FechaRegistro = DateTime.Now;
                        notasActualizadas++;
                        Console.WriteLine($"✅ Nota actualizada: Actividad {notaData.ActividadAcademicaId}, Valor {notaData.Nota}");
                    }
                    else
                    {
                        // Crear nueva nota si no existe
                        var nuevaNota = new Notas
                        {
                            MateriasInscritasId = request.MateriasInscritasId,
                            ActividadAcademicaId = notaData.ActividadAcademicaId,
                            Nota = notaData.Nota,
                            FechaRegistro = DateTime.Now
                        };
                        
                        _context.Notas.Add(nuevaNota);
                        notasCreadas++;
                        Console.WriteLine($"🆕 Nota creada: Actividad {notaData.ActividadAcademicaId}, Valor {notaData.Nota}");
                    }
                }

                await _context.SaveChangesAsync();
                Console.WriteLine($"Total de notas actualizadas: {notasActualizadas}");
                Console.WriteLine($"Total de notas creadas: {notasCreadas}");

                // Actualizar el promedio en MateriasInscritas
                var materiaInscrita = await _context.MateriasInscritas
                    .Include(mi => mi.MateriasGrupo)
                        .ThenInclude(mg => mg.Grupo)
                    .Include(mi => mi.Notas)
                        .ThenInclude(n => n.ActividadAcademica)
                    .FirstOrDefaultAsync(mi => mi.MateriasInscritasId == request.MateriasInscritasId);

                if (materiaInscrita != null)
                {
                    // Obtener todas las actividades académicas del ciclo
                    var actividadesAcademicas = await _context.ActividadesAcademicas
                        .Where(a => a.CicloId == materiaInscrita.MateriasGrupo.Grupo.CicloId)
                        .ToListAsync();

                    // Calcular el promedio usando el mismo método compartido
                    materiaInscrita.NotaPromedio = CalcularPromedioMateriaComun(materiaInscrita.Notas, actividadesAcademicas);
                    
                    _context.MateriasInscritas.Update(materiaInscrita);
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"✅ Promedio actualizado: {materiaInscrita.NotaPromedio}");
                }

                string mensaje = "";
                if (notasActualizadas > 0 && notasCreadas > 0)
                {
                    mensaje = $"Se actualizaron {notasActualizadas} notas y se crearon {notasCreadas} notas correctamente";
                }
                else if (notasActualizadas > 0)
                {
                    mensaje = $"Se actualizaron {notasActualizadas} notas correctamente";
                }
                else if (notasCreadas > 0)
                {
                    mensaje = $"Se crearon {notasCreadas} notas correctamente";
                }
                else
                {
                    mensaje = "No se procesaron notas";
                }

                return new JsonResult(new { success = true, message = mensaje });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar notas: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return new JsonResult(new { success = false, message = "Error al guardar las notas: " + ex.Message });
            }
        }

        public class GuardarNotasRequest
        {
            public int AlumnoId { get; set; }
            public int MateriasGrupoId { get; set; }
            public int MateriasInscritasId { get; set; }
            public List<NotaSimple> Notas { get; set; } = new List<NotaSimple>();
        }

        public class NotaSimple
        {
            public int ActividadAcademicaId { get; set; }
            public decimal Nota { get; set; }
        }

        public async Task<IActionResult> OnPostAsync(int grupoId)
        {
            try
            {
                // Deshabilitar validación automática temporalmente
                ModelState.Clear();
                
                // Log para debugging
                Console.WriteLine($"POST recibido para grupoId: {grupoId}");
                Console.WriteLine($"NotasEditar es null: {NotasEditar == null}");
                Console.WriteLine($"NotasEditar count: {NotasEditar?.Count ?? 0}");

                // Validar que hay datos para procesar
                if (NotasEditar == null || !NotasEditar.Any())
                {
                    Console.WriteLine("No hay datos de notas para procesar");
                    TempData["ErrorMessage"] = "No hay datos de notas para procesar.";
                    await OnGetAsync(grupoId);
                    return Page();
                }

                Console.WriteLine("Procesando notas...");

                int notasProcesadas = 0;
                foreach (var alumnoNotas in NotasEditar.Values)
                {
                    foreach (var materiaNotas in alumnoNotas.Values)
                    {
                        foreach (var notaData in materiaNotas.Values)
                        {
                            Console.WriteLine($"Procesando nota: NotasId={notaData.NotasId}, Nota={notaData.Nota}, MateriasInscritasId={notaData.MateriasInscritasId}, ActividadAcademicaId={notaData.ActividadAcademicaId}");
                            
                            // Validación manual de la nota
                            if (notaData.Nota < 0 || notaData.Nota > 10)
                            {
                                Console.WriteLine($"Nota inválida: {notaData.Nota}");
                                continue;
                            }
                            
                            if (notaData.NotasId > 0)
                            {
                                // Actualizar nota existente
                                var notaExistente = await _context.Notas.FindAsync(notaData.NotasId);
                                if (notaExistente != null)
                                {
                                    notaExistente.Nota = notaData.Nota;
                                    notaExistente.FechaRegistro = DateTime.Now; // Actualizar fecha de modificación
                                    notasProcesadas++;
                                    Console.WriteLine($"Nota actualizada: ID={notaData.NotasId}, Nuevo valor={notaData.Nota}");
                                }
                                else
                                {
                                    Console.WriteLine($"No se encontró la nota con ID: {notaData.NotasId}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Nota con ID 0 ignorada (no se crean nuevas notas)");
                            }
                        }
                    }
                }
                
                Console.WriteLine($"Total de notas procesadas: {notasProcesadas}");

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Las notas se han actualizado correctamente.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en OnPostAsync: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "Error al actualizar las notas: " + ex.Message;
                await OnGetAsync(grupoId);
                return Page();
            }

            return RedirectToPage("./EditarNotasGrupo", new { grupoId });
        }
    }
}

