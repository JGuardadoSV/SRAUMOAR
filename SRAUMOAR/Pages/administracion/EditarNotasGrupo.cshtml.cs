using ClosedXML.Excel;
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace SRAUMOAR.Pages.administracion
{
    [Authorize(Roles = "Administrador,Administracion")]
    [IgnoreAntiforgeryToken]
    public class EditarNotasGrupoModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IWebHostEnvironment _environment;
        private static readonly XLColor NotaBackgroundColor = XLColor.FromHtml("#AED99E");

        public EditarNotasGrupoModel(SRAUMOAR.Modelos.Contexto context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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

            /// <summary>
            /// Calcula el promedio para una materia específica usando las notas de las materias inscritas
            /// </summary>
            public decimal CalcularPromedioMateria(int materiasGrupoId, IList<ActividadAcademica> actividadesAcademicas)
            {
                // Obtener la materia inscrita correspondiente
                var materiaInscrita = MateriasInscritas.FirstOrDefault(mi => mi.MateriasGrupoId == materiasGrupoId);
                if (materiaInscrita == null)
                    return 0;

                // Obtener las notas directamente de la materia inscrita
                var notasMateria = materiaInscrita.Notas ?? new List<Notas>();

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

        public async Task<IActionResult> OnGetExportarMateriaAsync(int grupoId, int materiasGrupoId)
        {
            var grupo = await _context.Grupo
                .Include(g => g.Carrera)
                .Include(g => g.Ciclo)
                .FirstOrDefaultAsync(g => g.GrupoId == grupoId);

            if (grupo == null)
            {
                return NotFound("El grupo solicitado no existe.");
            }

            var materiaGrupo = await _context.MateriasGrupo
                .Include(mg => mg.Materia)
                .Include(mg => mg.Docente)
                .FirstOrDefaultAsync(mg => mg.MateriasGrupoId == materiasGrupoId && mg.GrupoId == grupoId);

            if (materiaGrupo == null)
            {
                return NotFound("No se encontró la materia solicitada dentro del grupo.");
            }

            var actividades = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == grupo.CicloId)
                .OrderBy(a => a.FechaInicio)
                .ToListAsync();

            if (!actividades.Any())
            {
                return NotFound("No hay actividades académicas configuradas para el ciclo actual.");
            }

            var estudiantes = await _context.MateriasInscritas
                .Include(mi => mi.Alumno)
                .Include(mi => mi.Notas!)
                    .ThenInclude(n => n.ActividadAcademica)
                .Where(mi => mi.MateriasGrupoId == materiasGrupoId)
                .OrderBy(mi => mi.Alumno!.Apellidos)
                .ThenBy(mi => mi.Alumno!.Nombres)
                .ToListAsync();

            var archivo = GenerarExcelMateria(grupo, materiaGrupo, actividades, estudiantes);
            var nombreArchivo = $"Notas_{materiaGrupo.Materia?.NombreMateria ?? "Materia"}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(archivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
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

        private byte[] GenerarExcelMateria(Grupo grupo, MateriasGrupo materiaGrupo, IList<ActividadAcademica> actividades, IList<MateriasInscritas> estudiantes)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Notas");

            // Insertar logo primero
            InsertarLogo(worksheet);

            ConstruirEncabezadoGeneral(worksheet, grupo, materiaGrupo);

            var actividadesReposicion = actividades
                .Where(a => EsActividadReposicion(a))
                .ToList();
            var actividadReposicion = actividadesReposicion.FirstOrDefault();
            var actividadesPrincipales = actividades
                .Where(a => !EsActividadReposicion(a))
                .ToList();

            var columnasActividades = ConstruirEncabezadoTabla(worksheet, actividadesPrincipales, out var columnaTotal, out var columnaReposicion, out var columnaNotaFinal, out var columnaObservaciones);
            var ultimaFila = LlenarDetalleTabla(worksheet, actividadesPrincipales, actividadReposicion, estudiantes, columnasActividades, columnaTotal, columnaReposicion, columnaNotaFinal, columnaObservaciones);

            AplicarFormatoTabla(worksheet, ultimaFila, columnaObservaciones, columnaTotal, columnaReposicion, columnaNotaFinal, columnaObservaciones);
            InsertarPieDePagina(worksheet, ultimaFila, estudiantes);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream.ToArray();
        }

        private void ConstruirEncabezadoGeneral(IXLWorksheet worksheet, Grupo grupo, MateriasGrupo materiaGrupo)
        {
            worksheet.Range(1, 3, 1, 19).Merge();
            var cellUniversidad = worksheet.Cell(1, 3);
            cellUniversidad.Value = "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO";
            cellUniversidad.Style.Font.FontSize = 16;
            cellUniversidad.Style.Font.Bold = true;
            cellUniversidad.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Range(2, 3, 2, 19).Merge();
            var cellRegistro = worksheet.Cell(2, 3);
            cellRegistro.Value = "ADMINISTRACIÓN DE REGISTRO ACADÉMICO";
            cellRegistro.Style.Font.FontSize = 16;
            cellRegistro.Style.Font.Bold = true;
            cellRegistro.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Range(3, 3, 3, 10).Merge();
            worksheet.Cell(3, 3).Value = $"CUADROS DE CALIFICACIONES FINALES DEL CICLO {grupo.Ciclo?.NCiclo}/{grupo.Ciclo?.anio}";
            worksheet.Range(4, 3, 4, 10).Merge();
            worksheet.Cell(4, 3).Value = (grupo.Carrera?.NombreCarrera ?? string.Empty).ToUpperInvariant();

            worksheet.Range(3, 3, 4, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Range(3, 3, 4, 10).Style.Font.Bold = true;

            // Bloque derecho: información de la materia en formato "ETIQUETA: VALOR" en una sola celda combinada por fila
            int labelColumn = 11; // Columna K
            
            // Fila 3: ASIGNATURA
            var rangoAsignatura = worksheet.Range(3, labelColumn, 3, 19);
            rangoAsignatura.Merge();
            rangoAsignatura.Value = $"ASIGNATURA: {materiaGrupo.Materia?.NombreMateria ?? "-"}";
            rangoAsignatura.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            rangoAsignatura.Style.Font.Bold = true;

            // Fila 4: CÓDIGO
            var rangoCodigo = worksheet.Range(4, labelColumn, 4, 19);
            rangoCodigo.Merge();
            rangoCodigo.Value = $"CÓDIGO: {materiaGrupo.Materia?.CodigoMateria ?? "-"}";
            rangoCodigo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            // Fila 5: DÍA
            var rangoDia = worksheet.Range(5, labelColumn, 5, 19);
            rangoDia.Merge();
            rangoDia.Value = $"DÍA: {materiaGrupo.Dia.GetDisplayName()}";
            rangoDia.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            // Fila 6: HORARIO
            var rangoHorario = worksheet.Range(6, labelColumn, 6, 19);
            rangoHorario.Merge();
            rangoHorario.Value = $"HORARIO: {materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraInicio)} a {materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraFin)}";
            rangoHorario.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            // Fila 7: CATEDRÁTICO
            var rangoDocente = worksheet.Range(7, labelColumn, 7, 19);
            rangoDocente.Merge();
            var nombreDocente = materiaGrupo.Docente != null ? $"{materiaGrupo.Docente.Nombres} {materiaGrupo.Docente.Apellidos}".Trim() : "Sin asignar";
            rangoDocente.Value = $"CATEDRÁTICO: {nombreDocente}";
            rangoDocente.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        private Dictionary<int, (int notaColumna, int ponderacionColumna)> ConstruirEncabezadoTabla(
            IXLWorksheet worksheet,
            IList<ActividadAcademica> actividades,
            out int columnaTotalPuntos,
            out int columnaReposicion,
            out int columnaNotaFinal,
            out int columnaObservaciones)
        {
            const int filaEncabezado = 9;
            const int filaSubEncabezado = 10;

            var headersSimples = new[]
            {
                (Titulo: "N°", Columna: 1),
                (Titulo: "N° CARNET", Columna: 2),
                (Titulo: "NOMBRE EN ORDEN", Columna: 3)
            };

            foreach (var header in headersSimples)
            {
                var rango = worksheet.Range(filaEncabezado, header.Columna, filaSubEncabezado, header.Columna);
                rango.Merge();
                rango.Value = header.Titulo;
                rango.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                rango.Style.Font.Bold = true;
                rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int columnaActual = 4;
            var mapaColumnas = new Dictionary<int, (int notaColumna, int ponderacionColumna)>();

            foreach (var actividad in actividades)
            {
                var rangoTitulo = worksheet.Range(filaEncabezado, columnaActual, filaEncabezado, columnaActual + 1);
                rangoTitulo.Merge();
                var nombreActividad = ObtenerNombreCompletoActividad(actividad);
                rangoTitulo.Value = nombreActividad.ToUpperInvariant();
                rangoTitulo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rangoTitulo.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                rangoTitulo.Style.Alignment.WrapText = true;
                rangoTitulo.Style.Font.Bold = true;
                rangoTitulo.Style.Font.FontSize = 9;
                rangoTitulo.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                worksheet.Column(columnaActual).Width = 7;
                worksheet.Column(columnaActual + 1).Width = 7;

                worksheet.Cell(filaSubEncabezado, columnaActual).Value = "NOTA";
                worksheet.Cell(filaSubEncabezado, columnaActual + 1).Value = $"{actividad.Porcentaje}%";
                worksheet.Cell(filaSubEncabezado, columnaActual).Style.Font.FontSize = 9;
                worksheet.Cell(filaSubEncabezado, columnaActual + 1).Style.Font.FontSize = 9;

                worksheet.Range(filaSubEncabezado, columnaActual, filaSubEncabezado, columnaActual + 1)
                    .Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Range(filaSubEncabezado, columnaActual, filaSubEncabezado, columnaActual + 1)
                    .Style.Font.Bold = true;
                worksheet.Range(filaSubEncabezado, columnaActual, filaSubEncabezado, columnaActual + 1)
                    .Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                mapaColumnas[actividad.ActividadAcademicaId] = (columnaActual, columnaActual + 1);
                columnaActual += 2;
            }

            var columnasFinales = new Dictionary<string, string>
            {
                { "TOTAL DE PUNTOS", $"TOTAL DE{Environment.NewLine}PUNTOS" },
                { "NOTA DE REPOSICIÓN", $"NOTA DE{Environment.NewLine}REPOSICIÓN" },
                { "NOTA FINAL", $"NOTA{Environment.NewLine}FINAL" },
                { "OBSERVACIONES", $"OBSERVA{Environment.NewLine}CIONES" }
            };

            var indicesFinales = new Dictionary<string, int>();
            foreach (var finalCol in columnasFinales)
            {
                var rango = worksheet.Range(filaEncabezado, columnaActual, filaSubEncabezado, columnaActual);
                rango.Merge();
                rango.Value = finalCol.Value;
                rango.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                rango.Style.Alignment.WrapText = true;
                rango.Style.Font.Bold = true;
                rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                indicesFinales[finalCol.Key] = columnaActual;
                columnaActual++;
            }

            columnaTotalPuntos = indicesFinales["TOTAL DE PUNTOS"];
            columnaReposicion = indicesFinales["NOTA DE REPOSICIÓN"];
            columnaNotaFinal = indicesFinales["NOTA FINAL"];
            columnaObservaciones = indicesFinales["OBSERVACIONES"];

            return mapaColumnas;
        }

        private int LlenarDetalleTabla(
            IXLWorksheet worksheet,
            IList<ActividadAcademica> actividades,
            ActividadAcademica? actividadReposicion,
            IList<MateriasInscritas> estudiantes,
            Dictionary<int, (int notaColumna, int ponderacionColumna)> mapaColumnas,
            int columnaTotal,
            int columnaReposicion,
            int columnaNotaFinal,
            int columnaObservaciones)
        {
            int filaActual = 11;
            int correlativo = 1;

            foreach (var materiaInscrita in estudiantes)
            {
                var alumno = materiaInscrita.Alumno;
                worksheet.Cell(filaActual, 1).Value = correlativo++;
                worksheet.Cell(filaActual, 2).Value = ObtenerCarnet(alumno);
                worksheet.Cell(filaActual, 3).Value = $"{alumno?.Apellidos} {alumno?.Nombres}".Trim();

                decimal totalPuntos = 0;
                foreach (var actividad in actividades)
                {
                    if (!mapaColumnas.TryGetValue(actividad.ActividadAcademicaId, out var columnas))
                    {
                        continue;
                    }

                    var nota = materiaInscrita.Notas?
                        .FirstOrDefault(n => n.ActividadAcademicaId == actividad.ActividadAcademicaId)?.Nota ?? 0;

                    var notaCell = worksheet.Cell(filaActual, columnas.notaColumna);
                    notaCell.Value = nota;
                    notaCell.Style.NumberFormat.Format = "0.00";
                    notaCell.Style.Fill.BackgroundColor = NotaBackgroundColor;

                    var ponderado = Math.Round(nota * actividad.Porcentaje / 100m, 2);
                    worksheet.Cell(filaActual, columnas.ponderacionColumna).Value = ponderado;
                    worksheet.Cell(filaActual, columnas.ponderacionColumna).Style.NumberFormat.Format = "0.00";

                    totalPuntos += ponderado;
                }

                worksheet.Cell(filaActual, columnaTotal).Value = Math.Round(totalPuntos, 2);
                worksheet.Cell(filaActual, columnaTotal).Style.NumberFormat.Format = "0.00";

                // Usar la nota de recuperación del registro (campo NotaRecuperacion)
                decimal notaRecuperacion = materiaInscrita.NotaRecuperacion ?? 0;
                worksheet.Cell(filaActual, columnaReposicion).Value = notaRecuperacion;
                worksheet.Cell(filaActual, columnaReposicion).Style.NumberFormat.Format = "0.00";

                // Calcular nota final para el Excel
                decimal notaFinal;
                if (materiaInscrita.NotaRecuperacion.HasValue && materiaInscrita.NotaRecuperacion.Value >= 7)
                {
                    // Si aprobó recuperación (>=7), la nota final es 7
                    notaFinal = 7;
                }
                else if (materiaInscrita.NotaRecuperacion.HasValue)
                {
                    // Si tiene nota de recuperación pero reprobó (<7), usar esa nota
                    notaFinal = materiaInscrita.NotaRecuperacion.Value;
                }
                else
                {
                    // Si no tiene nota de recuperación, usar el promedio
                    notaFinal = materiaInscrita.NotaPromedio > 0 ? materiaInscrita.NotaPromedio : totalPuntos;
                }

                var notaFinalRedondeada = Math.Round(notaFinal * 10m, 0, MidpointRounding.AwayFromZero) / 10m;
                worksheet.Cell(filaActual, columnaNotaFinal).Value = notaFinalRedondeada;
                worksheet.Cell(filaActual, columnaNotaFinal).Style.NumberFormat.Format = "0.00";

                filaActual++;
            }

            return filaActual - 1;
        }

        private void AplicarFormatoTabla(IXLWorksheet worksheet, int ultimaFila, int ultimaColumna, int columnaTotal, int columnaReposicion, int columnaNotaFinal, int columnaObservaciones)
        {
            if (ultimaFila < 11)
            {
                ultimaFila = 11;
            }

            var rango = worksheet.Range(9, 1, ultimaFila, ultimaColumna);
            rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rango.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            rango.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            worksheet.Column(3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            worksheet.Row(9).Height = 32;
            worksheet.Row(10).Height = 20;

            // ========================================
            // BLOQUE DE ANCHOS DE COLUMNAS - AJUSTAR MANUALMENTE AQUÍ
            // ========================================
            worksheet.Column(1).Width = 4;      // N°
            worksheet.Column(2).Width = 10;     // N° CARNET
            worksheet.Column(3).Width = 35;     // NOMBRE EN ORDEN
            worksheet.Column(4).Width = 6;      // PRIMER LAB - NOTA
            worksheet.Column(5).Width = 6;      // PRIMER LAB - %
            worksheet.Column(6).Width = 6;      // PRIMER PARCIAL - NOTA
            worksheet.Column(7).Width = 6;      // PRIMER PARCIAL - %
            worksheet.Column(8).Width = 6;      // SEGUNDO LAB - NOTA
            worksheet.Column(9).Width = 6;      // SEGUNDO LAB - %
            worksheet.Column(10).Width = 6;     // SEGUNDO PARCIAL - NOTA
            worksheet.Column(11).Width = 6;     // SEGUNDO PARCIAL - %
            worksheet.Column(12).Width = 6;     // TERCER LAB - NOTA
            worksheet.Column(13).Width = 6;     // TERCER LAB - %
            worksheet.Column(14).Width = 6;     // TERCER PARCIAL - NOTA
            worksheet.Column(15).Width = 6;     // TERCER PARCIAL - %
            worksheet.Column(16).Width = 10;    // TOTAL DE PUNTOS
            worksheet.Column(17).Width = 12;    // NOTA DE REPOSICIÓN
            worksheet.Column(18).Width = 8;     // NOTA FINAL
            worksheet.Column(19).Width = 14;    // OBSERVACIONES
            // ========================================
        }

        private void InsertarLogo(IXLWorksheet worksheet)
        {
            try
            {
                // Intentar múltiples rutas posibles
                var posiblesRutas = new List<string>
                {
                    Path.Combine(_environment.WebRootPath ?? "", "images", "logoUmoar.jpg"),
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoUmoar.jpg"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "logoUmoar.jpg"),
                    @"C:\Users\Facturacion\source\repos\JGuardadoSV\SRAUMOAR\SRAUMOAR\wwwroot\images\logoUmoar.jpg"
                };

                string? logoPath = null;
                foreach (var ruta in posiblesRutas)
                {
                    Console.WriteLine($"Probando ruta: {ruta}, Exists: {System.IO.File.Exists(ruta)}");
                    if (System.IO.File.Exists(ruta))
                    {
                        logoPath = ruta;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(logoPath))
                {
                    Console.WriteLine($"Usando logo desde: {logoPath}");
                    
                    // Convertir JPG a PNG en memoria usando ImageSharp
                    using var image = SixLabors.ImageSharp.Image.Load(logoPath);
                    using var pngStream = new MemoryStream();
                    image.SaveAsPng(pngStream);
                    pngStream.Position = 0;
                    
                    var picture = worksheet.AddPicture(pngStream, "Logo");
                    picture.MoveTo(worksheet.Cell(1, 1));
                    picture.Width = 80;
                    picture.Height = 80;
                    
                    Console.WriteLine("Logo insertado correctamente");
                }
                else
                {
                    Console.WriteLine("No se encontró el archivo del logo en ninguna ruta");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al insertar logo: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        private void InsertarPieDePagina(IXLWorksheet worksheet, int filaInicio, IList<MateriasInscritas> estudiantes)
        {
            // Contar aprobados y reprobados por género
            int aprobadosMasculinos = 0;
            int aprobadosFemeninos = 0;
            int reprobadosMasculinos = 0;
            int reprobadosFemeninos = 0;
            int retiradosMasculinos = 0;
            int retiradosFemeninos = 0;

            foreach (var mi in estudiantes)
            {
                var alumno = mi.Alumno;
                if (alumno == null) continue;

                bool esMasculino = alumno.Genero == 0; // 0 = Masculino, 1 = Femenino
                // Si hay nota de recuperación, esa determina si aprobó (nota mínima 7)
                var notaFinal = mi.NotaRecuperacion ?? mi.NotaPromedio;
                bool aprobado = notaFinal >= 7;

                if (aprobado)
                {
                    if (esMasculino) aprobadosMasculinos++;
                    else aprobadosFemeninos++;
                }
                else
                {
                    if (esMasculino) reprobadosMasculinos++;
                    else reprobadosFemeninos++;
                }
            }

            int filaActual = filaInicio + 2;

            // Fila de aprobados
            worksheet.Cell(filaActual, 1).Value = $"APROBADOS : MASCULINOS:__{aprobadosMasculinos}___FEMENINOS :__{aprobadosFemeninos}__";
            worksheet.Range(filaActual, 1, filaActual, 8).Merge();
            
            worksheet.Cell(filaActual, 12).Value = $"FECHA DE ENTREGA : {DateTime.Now:dd/MM/yyyy}";
            worksheet.Range(filaActual, 12, filaActual, 19).Merge();
            
            filaActual++;

            // Fila de reprobados
            worksheet.Cell(filaActual, 1).Value = $"REPROBADOS : MASCULINOS :__{reprobadosMasculinos}___FEMENINOS :__{reprobadosFemeninos}__";
            worksheet.Range(filaActual, 1, filaActual, 8).Merge();
            
            worksheet.Cell(filaActual, 12).Value = "FIRMA DEL DOCENTE :________________________";
            worksheet.Range(filaActual, 12, filaActual, 19).Merge();
            
            filaActual++;

            // Fila de retirados
            worksheet.Cell(filaActual, 1).Value = $"RETIRADOS : MASCULINO :__{retiradosMasculinos}__ FEMENINO__{retiradosFemeninos}__";
            worksheet.Range(filaActual, 1, filaActual, 8).Merge();
        }

        private static bool EsActividadReposicion(ActividadAcademica? actividad)
        {
            if (actividad?.Nombre == null)
            {
                return false;
            }

            return actividad.Nombre.Contains("repos", StringComparison.OrdinalIgnoreCase);
        }

        private static string ObtenerCarnet(Alumno? alumno)
        {
            if (alumno == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(alumno.Carnet))
            {
                return alumno.Carnet;
            }

            if (!string.IsNullOrWhiteSpace(alumno.Email))
            {
                var parts = alumno.Email.Split('@');
                if (parts.Length > 0)
                {
                    return parts[0];
                }
            }

            return string.Empty;
        }

        private static string ObtenerNombreCompletoActividad(ActividadAcademica actividad)
        {
            if (actividad == null || string.IsNullOrWhiteSpace(actividad.Nombre))
            {
                return string.Empty;
            }

            var nombre = actividad.Nombre.Trim();

            // Mapear abreviaturas comunes a nombres completos
            var mapeoNombres = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "LAB 1", "PRIMER LABORATORIO" },
                { "LAB1", "PRIMER LABORATORIO" },
                { "PAR 1", "PRIMER PARCIAL" },
                { "PAR1", "PRIMER PARCIAL" },
                { "LAB 2", "SEGUNDO LABORATORIO" },
                { "LAB2", "SEGUNDO LABORATORIO" },
                { "PAR 2", "SEGUNDO PARCIAL" },
                { "PAR2", "SEGUNDO PARCIAL" },
                { "LAB 3", "TERCER LABORATORIO" },
                { "LAB3", "TERCER LABORATORIO" },
                { "PAR 3", "TERCER PARCIAL" },
                { "PAR3", "TERCER PARCIAL" }
            };

            if (mapeoNombres.TryGetValue(nombre, out var nombreCompleto))
            {
                return nombreCompleto;
            }

            return nombre;
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

