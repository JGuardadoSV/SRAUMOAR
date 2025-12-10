using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Alumnos;
using System.ComponentModel.DataAnnotations;
using System.IO;
using ClosedXML.Excel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Layout.Borders;
using iText.Kernel.Colors;

namespace SRAUMOAR.Pages.grupos
{
    public class DetailsModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IWebHostEnvironment _environment;
        private static readonly XLColor NotaBackgroundColor = XLColor.FromHtml("#AED99E");

        public DetailsModel(SRAUMOAR.Modelos.Contexto context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public Grupo Grupo { get; set; } = default!;
        public IList<Alumno> AlumnosInscritos { get; set; } = new List<Alumno>();
        public IList<MateriaDelGrupo> MateriasDelGrupo { get; set; } = new List<MateriaDelGrupo>();
        public IList<ActividadAcademica> ActividadesAcademicas { get; set; } = new List<ActividadAcademica>();
        public IList<EstudianteConNotas> EstudiantesConNotas { get; set; } = new List<EstudianteConNotas>();

        public class EstudianteConNotas
        {
            public int AlumnoId { get; set; }
            public string NombreCompleto { get; set; } = string.Empty; // Apellidos Nombres
            public IList<Notas> Notas { get; set; } = new List<Notas>();
            public List<MateriasInscritas> MateriasInscritas { get; set; } = new List<MateriasInscritas>();
        }

        public class MateriaDelGrupo
        {
            public int MateriasGrupoId { get; set; }
            public string NombreMateria { get; set; } = string.Empty;
            public string CodigoMateria { get; set; } = string.Empty;
        }

        /// <summary>
        /// Calcula el promedio ponderado de notas usando los porcentajes configurados en las actividades académicas
        /// </summary>
        public static decimal CalcularPromedioMateriaComun(ICollection<Notas> notas, IList<ActividadAcademica> actividadesAcademicas)
        {
            if (actividadesAcademicas == null || !actividadesAcademicas.Any())
                return 0;

            decimal sumaPonderada = 0;
            decimal totalPorcentaje = 0;

            foreach (var actividad in actividadesAcademicas)
            {
                if (actividad == null) continue;

                int porcentaje = actividad.Porcentaje;
                totalPorcentaje += porcentaje;

                var notaRegistrada = notas?.FirstOrDefault(n => n.ActividadAcademicaId == actividad.ActividadAcademicaId);
                decimal valorNota = notaRegistrada?.Nota ?? 0;
                sumaPonderada += valorNota * porcentaje;
            }

            if (totalPorcentaje <= 0) return 0;

            return Math.Round(sumaPonderada / totalPorcentaje, 2);
        }

        /// <summary>
        /// Calcula la nota final aplicando las reglas de reposición
        /// </summary>
        public static decimal CalcularNotaFinal(MateriasInscritas materiaInscrita, ICollection<Notas> notasMateria, IList<ActividadAcademica> actividadesAcademicas)
        {
            // Calcular promedio base
            decimal promedio = CalcularPromedioMateriaComun(notasMateria, actividadesAcademicas);

            // Aplicar regla de reposición
            if (materiaInscrita.NotaRecuperacion.HasValue)
            {
                if (materiaInscrita.NotaRecuperacion.Value >= 7)
                {
                    // Si aprobó recuperación (>=7), la nota final es 7
                    return 7;
                }
                else
                {
                    // Si tiene nota de recuperación pero reprobó (<7), usar esa nota
                    return materiaInscrita.NotaRecuperacion.Value;
                }
            }

            // Si no tiene nota de recuperación, usar el promedio calculado
            return promedio;
        }

        public bool EsConsultaHistorica { get; set; } = false;
        public int? CicloIdSeleccionado { get; set; }
        public string? BuscarAlumno { get; set; }
        public Entidades.Alumnos.Alumno? AlumnoFiltrado { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id, int? cicloId = null, string? buscarAlumno = null)
        {
            if (id == null)
            {
                return NotFound();
            }

            var grupo = await _context.Grupo
                .Include(g => g.Carrera)
                .Include(g => g.Ciclo)
                .Include(g => g.Docente)
                .FirstOrDefaultAsync(m => m.GrupoId == id);
            if (grupo == null)
            {
                return NotFound();
            }

            // Si se proporciona cicloId, validar que el grupo pertenezca a ese ciclo
            if (cicloId.HasValue)
            {
                if (grupo.CicloId != cicloId.Value)
                {
                    return NotFound(); // El grupo no pertenece al ciclo seleccionado
                }
                EsConsultaHistorica = true;
                CicloIdSeleccionado = cicloId.Value;
            }
            else
            {
                EsConsultaHistorica = false;
            }

            Grupo = grupo;
            BuscarAlumno = buscarAlumno;

            // Si hay búsqueda de alumno, buscar el alumno
            if (!string.IsNullOrWhiteSpace(BuscarAlumno))
            {
                var terminoBusqueda = BuscarAlumno.Trim();
                
                // Buscar por carnet O por parte antes del @ del email
                var alumnos = await _context.Alumno
                    .Where(a => 
                        (!string.IsNullOrEmpty(a.Carnet) && a.Carnet.Contains(terminoBusqueda)) ||
                        (!string.IsNullOrEmpty(a.Email) && a.Email.Contains(terminoBusqueda))
                    )
                    .ToListAsync();
                
                // Filtrar en memoria: buscar por carnet O por parte antes del @ del email
                AlumnoFiltrado = alumnos.FirstOrDefault(a => 
                {
                    // 1. Buscar por carnet (si tiene carnet y contiene el término)
                    if (!string.IsNullOrEmpty(a.Carnet) && a.Carnet.Contains(terminoBusqueda))
                        return true;
                    
                    // 2. Buscar por parte antes del @ del email (si tiene email y contiene @)
                    if (!string.IsNullOrEmpty(a.Email) && a.Email.Contains("@"))
                    {
                        var parteEmail = a.Email.Substring(0, a.Email.IndexOf("@"));
                        if (parteEmail.Contains(terminoBusqueda))
                            return true;
                    }
                    
                    return false;
                });
            }

            // Materias del grupo
            MateriasDelGrupo = await _context.MateriasGrupo
                .Include(mg => mg.Materia)
                .Where(mg => mg.GrupoId == Grupo.GrupoId)
                .Select(mg => new MateriaDelGrupo
                {
                    MateriasGrupoId = mg.MateriasGrupoId,
                    NombreMateria = mg.Materia!.NombreMateria!,
                    CodigoMateria = mg.Materia!.CodigoMateria!
                })
                .ToListAsync();

            // Actividades académicas del ciclo del grupo, ordenadas por FechaInicio
            ActividadesAcademicas = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == Grupo.CicloId)
                .OrderBy(a => a.FechaInicio)
                .ToListAsync();

            // Estudiantes con notas por grupo
            var materiasInscritas = await _context.MateriasInscritas
                .Include(mi => mi.Alumno)
                .Include(mi => mi.MateriasGrupo)
                .Include(mi => mi.Notas) .ThenInclude(n => n.ActividadAcademica)
                .Where(mi => mi.MateriasGrupo!.GrupoId == Grupo.GrupoId)
                .ToListAsync();

            // Lista simple de alumnos inscritos (para otros usos)
            AlumnosInscritos = materiasInscritas
                .Where(mi => mi.Alumno != null)
                .Select(mi => mi.Alumno!)
                .Distinct()
                .OrderBy(a => a.Apellidos).ThenBy(a => a.Nombres)
                .ToList();

            EstudiantesConNotas = materiasInscritas
                .GroupBy(mi => mi.AlumnoId)
                .Select(g => new EstudianteConNotas
                {
                    AlumnoId = g.Key,
                    NombreCompleto = ($"{g.First().Alumno!.Apellidos} {g.First().Alumno!.Nombres}").Trim(),
                    Notas = g.SelectMany(mi => mi.Notas ?? new List<Notas>()).ToList(),
                    MateriasInscritas = g.ToList()
                })
                .OrderBy(e => e.NombreCompleto)
                .ToList();

            // Si hay filtro de alumno, filtrar los estudiantes mostrados
            if (AlumnoFiltrado != null)
            {
                EstudiantesConNotas = EstudiantesConNotas
                    .Where(e => e.AlumnoId == AlumnoFiltrado.AlumnoId)
                    .ToList();
            }

            return Page();
        }

        public async Task<IActionResult> OnGetExportarMateriaAsync(int grupoId, int materiasGrupoId, int? cicloId = null, string? buscarAlumno = null)
        {
            var grupo = await _context.Grupo
                .Include(g => g.Carrera)
                .Include(g => g.Ciclo)
                .FirstOrDefaultAsync(g => g.GrupoId == grupoId);

            if (grupo == null)
            {
                return NotFound("El grupo solicitado no existe.");
            }

            // Validar ciclo si se proporciona
            if (cicloId.HasValue && grupo.CicloId != cicloId.Value)
            {
                return NotFound("El grupo no pertenece al ciclo seleccionado.");
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
                return NotFound("No hay actividades académicas configuradas para este ciclo.");
            }

            var queryEstudiantes = _context.MateriasInscritas
                .Include(mi => mi.Alumno)
                .Include(mi => mi.Notas!)
                    .ThenInclude(n => n.ActividadAcademica)
                .Where(mi => mi.MateriasGrupoId == materiasGrupoId);

            // Si hay filtro de alumno, buscar y filtrar
            if (!string.IsNullOrWhiteSpace(buscarAlumno))
            {
                var terminoBusqueda = buscarAlumno.Trim();
                
                var alumnos = await _context.Alumno
                    .Where(a => 
                        (!string.IsNullOrEmpty(a.Carnet) && a.Carnet.Contains(terminoBusqueda)) ||
                        (!string.IsNullOrEmpty(a.Email) && a.Email.Contains(terminoBusqueda))
                    )
                    .ToListAsync();
                
                var alumnoFiltrado = alumnos.FirstOrDefault(a => 
                {
                    if (!string.IsNullOrEmpty(a.Carnet) && a.Carnet.Contains(terminoBusqueda))
                        return true;
                    
                    if (!string.IsNullOrEmpty(a.Email) && a.Email.Contains("@"))
                    {
                        var parteEmail = a.Email.Substring(0, a.Email.IndexOf("@"));
                        if (parteEmail.Contains(terminoBusqueda))
                            return true;
                    }
                    
                    return false;
                });

                if (alumnoFiltrado != null)
                {
                    queryEstudiantes = queryEstudiantes.Where(mi => mi.AlumnoId == alumnoFiltrado.AlumnoId);
                }
                else
                {
                    // Si no se encuentra el alumno, retornar lista vacía
                    queryEstudiantes = queryEstudiantes.Where(mi => false);
                }
            }

            var estudiantes = await queryEstudiantes
                .OrderBy(mi => mi.Alumno!.Apellidos)
                .ThenBy(mi => mi.Alumno!.Nombres)
                .ToListAsync();

            // Generar el Excel
            var archivo = GenerarExcelMateriaHistorico(grupo, materiaGrupo, actividades, estudiantes);
            var nombreArchivo = $"Notas_{materiaGrupo.Materia?.NombreMateria ?? "Materia"}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(archivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
        }

        public async Task<IActionResult> OnGetExportarMateriaPdfAsync(int grupoId, int materiasGrupoId, int? cicloId = null, string? buscarAlumno = null)
        {
            var grupo = await _context.Grupo
                .Include(g => g.Carrera)
                .Include(g => g.Ciclo)
                .FirstOrDefaultAsync(g => g.GrupoId == grupoId);

            if (grupo == null)
            {
                return NotFound("El grupo solicitado no existe.");
            }

            // Validar ciclo si se proporciona
            if (cicloId.HasValue && grupo.CicloId != cicloId.Value)
            {
                return NotFound("El grupo no pertenece al ciclo seleccionado.");
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
                return NotFound("No hay actividades académicas configuradas para este ciclo.");
            }

            var queryEstudiantes = _context.MateriasInscritas
                .Include(mi => mi.Alumno)
                .Include(mi => mi.Notas!)
                    .ThenInclude(n => n.ActividadAcademica)
                .Where(mi => mi.MateriasGrupoId == materiasGrupoId);

            // Si hay filtro de alumno, buscar y filtrar
            if (!string.IsNullOrWhiteSpace(buscarAlumno))
            {
                var terminoBusqueda = buscarAlumno.Trim();
                
                var alumnos = await _context.Alumno
                    .Where(a => 
                        (!string.IsNullOrEmpty(a.Carnet) && a.Carnet.Contains(terminoBusqueda)) ||
                        (!string.IsNullOrEmpty(a.Email) && a.Email.Contains(terminoBusqueda))
                    )
                    .ToListAsync();
                
                var alumnoFiltrado = alumnos.FirstOrDefault(a => 
                {
                    if (!string.IsNullOrEmpty(a.Carnet) && a.Carnet.Contains(terminoBusqueda))
                        return true;
                    
                    if (!string.IsNullOrEmpty(a.Email) && a.Email.Contains("@"))
                    {
                        var parteEmail = a.Email.Substring(0, a.Email.IndexOf("@"));
                        if (parteEmail.Contains(terminoBusqueda))
                            return true;
                    }
                    
                    return false;
                });

                if (alumnoFiltrado != null)
                {
                    queryEstudiantes = queryEstudiantes.Where(mi => mi.AlumnoId == alumnoFiltrado.AlumnoId);
                }
                else
                {
                    // Si no se encuentra el alumno, retornar lista vacía
                    queryEstudiantes = queryEstudiantes.Where(mi => false);
                }
            }

            var estudiantes = await queryEstudiantes
                .OrderBy(mi => mi.Alumno!.Apellidos)
                .ThenBy(mi => mi.Alumno!.Nombres)
                .ToListAsync();

            // Generar el PDF
            var archivo = GenerarPdfMateriaHistorico(grupo, materiaGrupo, actividades, estudiantes);
            var nombreArchivo = $"Notas_{materiaGrupo.Materia?.NombreMateria ?? "Materia"}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

            return File(archivo, "application/pdf", nombreArchivo);
        }

        private byte[] GenerarExcelMateriaHistorico(Grupo grupo, MateriasGrupo materiaGrupo, IList<ActividadAcademica> actividades, IList<MateriasInscritas> estudiantes)
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
                    if (System.IO.File.Exists(ruta))
                    {
                        logoPath = ruta;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(logoPath))
                {
                    // Convertir JPG a PNG en memoria usando ImageSharp
                    using var image = SixLabors.ImageSharp.Image.Load(logoPath);
                    using var pngStream = new MemoryStream();
                    image.SaveAsPng(pngStream);
                    pngStream.Position = 0;
                    
                    var picture = worksheet.AddPicture(pngStream, "Logo");
                    picture.MoveTo(worksheet.Cell(1, 1));
                    picture.Width = 80;
                    picture.Height = 80;
                }
            }
            catch (Exception ex)
            {
                // Si falla la inserción del logo, continuar sin él
                Console.WriteLine($"Error al insertar logo: {ex.Message}");
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

        private byte[] GenerarPdfMateriaHistorico(Grupo grupo, MateriasGrupo materiaGrupo, IList<ActividadAcademica> actividades, IList<MateriasInscritas> estudiantes)
        {
            var actividadesReposicion = actividades
                .Where(a => EsActividadReposicion(a))
                .ToList();
            var actividadReposicion = actividadesReposicion.FirstOrDefault();
            var actividadesPrincipales = actividades
                .Where(a => !EsActividadReposicion(a))
                .ToList();

            PdfFont fontNormal = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            PdfFont fontBold = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);

            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            {
                using var pdf = new PdfDocument(writer);
                var doc = new Document(pdf, iText.Kernel.Geom.PageSize.LETTER.Rotate());
                doc.SetMargins(20, 20, 20, 20);

                // Insertar logo
                try
                {
                    var posiblesRutas = new List<string>
                    {
                        Path.Combine(_environment.WebRootPath ?? "", "images", "logoUmoar.jpg"),
                        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoUmoar.jpg"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "logoUmoar.jpg")
                    };

                    string? logoPath = null;
                    foreach (var ruta in posiblesRutas)
                    {
                        if (System.IO.File.Exists(ruta))
                        {
                            logoPath = ruta;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(logoPath))
                    {
                        var logo = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(logoPath));
                        logo.SetWidth(60);
                        logo.SetFixedPosition(1, 20, doc.GetPageEffectiveArea(iText.Kernel.Geom.PageSize.LETTER.Rotate()).GetHeight() - 20);
                        doc.Add(logo);
                    }
                }
                catch { }

                // Encabezado
                var headerTable = new Table(1).UseAllAvailableWidth();
                headerTable.AddCell(new Cell()
                    .Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                        .SetFont(fontBold)
                        .SetFontSize(10)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetBorder(Border.NO_BORDER));
                headerTable.AddCell(new Cell()
                    .Add(new Paragraph("ADMINISTRACIÓN DE REGISTRO ACADÉMICO")
                        .SetFont(fontBold)
                        .SetFontSize(10)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetBorder(Border.NO_BORDER));
                headerTable.AddCell(new Cell()
                    .Add(new Paragraph($"CUADROS DE CALIFICACIONES FINALES DEL CICLO {grupo.Ciclo?.NCiclo}/{grupo.Ciclo?.anio}")
                        .SetFont(fontBold)
                        .SetFontSize(8)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetBorder(Border.NO_BORDER));
                headerTable.AddCell(new Cell()
                    .Add(new Paragraph((grupo.Carrera?.NombreCarrera ?? string.Empty).ToUpperInvariant())
                        .SetFont(fontBold)
                        .SetFontSize(8)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetBorder(Border.NO_BORDER));
                doc.Add(headerTable);

                // Información de la materia (lado derecho)
                var infoTable = new Table(2).UseAllAvailableWidth();
                infoTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph("")));
                var infoCell = new Cell().SetBorder(Border.NO_BORDER);
                infoCell.Add(new Paragraph($"ASIGNATURA: {materiaGrupo.Materia?.NombreMateria ?? "-"}").SetFont(fontBold).SetFontSize(7));
                infoCell.Add(new Paragraph($"CÓDIGO: {materiaGrupo.Materia?.CodigoMateria ?? "-"}").SetFont(fontNormal).SetFontSize(7));
                infoCell.Add(new Paragraph($"DÍA: {materiaGrupo.Dia.GetDisplayName()}").SetFont(fontNormal).SetFontSize(7));
                infoCell.Add(new Paragraph($"HORARIO: {materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraInicio)} a {materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraFin)}").SetFont(fontNormal).SetFontSize(7));
                var nombreDocente = materiaGrupo.Docente != null ? $"{materiaGrupo.Docente.Nombres} {materiaGrupo.Docente.Apellidos}".Trim() : "Sin asignar";
                infoCell.Add(new Paragraph($"CATEDRÁTICO: {nombreDocente}").SetFont(fontNormal).SetFontSize(7));
                infoTable.AddCell(infoCell);
                doc.Add(infoTable);

                doc.Add(new Paragraph(" ").SetFontSize(4));

                // Construir tabla de datos
                int numColumnas = 3 + (actividadesPrincipales.Count * 2) + 4; // N°, CARNET, NOMBRE + (NOTA, %) por actividad + TOTAL, REPOS, FINAL, OBS
                var columnas = new float[numColumnas];
                columnas[0] = 15; // N°
                columnas[1] = 30; // CARNET
                columnas[2] = 80; // NOMBRE
                int idx = 3;
                for (int i = 0; i < actividadesPrincipales.Count; i++)
                {
                    columnas[idx++] = 20; // NOTA
                    columnas[idx++] = 20; // %
                }
                columnas[idx++] = 30; // TOTAL
                columnas[idx++] = 30; // REPOS
                columnas[idx++] = 25; // FINAL
                columnas[idx++] = 40; // OBS

                var tabla = new Table(columnas).UseAllAvailableWidth();

                // Encabezados
                tabla.AddCell(CeldaEncabezadoPdf("N°", fontBold, 7));
                tabla.AddCell(CeldaEncabezadoPdf("N° CARNET", fontBold, 7));
                tabla.AddCell(CeldaEncabezadoPdf("NOMBRE EN ORDEN", fontBold, 7));

                foreach (var actividad in actividadesPrincipales)
                {
                    var nombreActividad = ObtenerNombreCompletoActividad(actividad);
                    var celda = new Cell(1, 2) // rowspan=1, colspan=2
                        .Add(new Paragraph(nombreActividad.ToUpperInvariant()).SetFont(fontBold).SetFontSize(6))
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                        .SetBorder(new SolidBorder(1))
                        .SetPadding(3);
                    tabla.AddCell(celda);
                }

                tabla.AddCell(CeldaEncabezadoPdf("TOTAL DE\nPUNTOS", fontBold, 6));
                tabla.AddCell(CeldaEncabezadoPdf("NOTA DE\nREPOSICIÓN", fontBold, 6));
                tabla.AddCell(CeldaEncabezadoPdf("NOTA\nFINAL", fontBold, 6));
                tabla.AddCell(CeldaEncabezadoPdf("OBSERVA\nCIONES", fontBold, 6));

                // Sub-encabezados
                tabla.AddCell(CeldaEncabezadoPdf("", fontBold, 6));
                tabla.AddCell(CeldaEncabezadoPdf("", fontBold, 6));
                tabla.AddCell(CeldaEncabezadoPdf("", fontBold, 6));

                foreach (var actividad in actividadesPrincipales)
                {
                    tabla.AddCell(CeldaEncabezadoPdf("NOTA", fontBold, 6));
                    tabla.AddCell(CeldaEncabezadoPdf($"{actividad.Porcentaje}%", fontBold, 6));
                }

                tabla.AddCell(CeldaEncabezadoPdf("", fontBold, 6));
                tabla.AddCell(CeldaEncabezadoPdf("", fontBold, 6));
                tabla.AddCell(CeldaEncabezadoPdf("", fontBold, 6));
                tabla.AddCell(CeldaEncabezadoPdf("", fontBold, 6));

                // Datos de estudiantes
                int correlativo = 1;
                var mapaColumnas = new Dictionary<int, int>();
                int colIdx = 3;
                foreach (var actividad in actividadesPrincipales)
                {
                    mapaColumnas[actividad.ActividadAcademicaId] = colIdx;
                    colIdx += 2;
                }

                foreach (var materiaInscrita in estudiantes)
                {
                    var alumno = materiaInscrita.Alumno;
                    tabla.AddCell(CeldaDatosPdf(correlativo++.ToString(), fontNormal, 7));
                    tabla.AddCell(CeldaDatosPdf(ObtenerCarnet(alumno), fontNormal, 7));
                    tabla.AddCell(CeldaDatosPdf($"{alumno?.Apellidos} {alumno?.Nombres}".Trim(), fontNormal, 7));

                    decimal totalPuntos = 0;
                    foreach (var actividad in actividadesPrincipales)
                    {
                        var nota = materiaInscrita.Notas?
                            .FirstOrDefault(n => n.ActividadAcademicaId == actividad.ActividadAcademicaId)?.Nota ?? 0;
                        var ponderado = Math.Round(nota * actividad.Porcentaje / 100m, 2);
                        totalPuntos += ponderado;

                        var notaCell = CeldaDatosPdf(nota.ToString("0.00"), fontNormal, 7);
                        notaCell.SetBackgroundColor(new DeviceRgb(174, 217, 158)); // Color similar al Excel
                        tabla.AddCell(notaCell);
                        tabla.AddCell(CeldaDatosPdf(ponderado.ToString("0.00"), fontNormal, 7));
                    }

                    decimal notaRecuperacion = materiaInscrita.NotaRecuperacion ?? 0;
                    decimal notaFinal;
                    if (materiaInscrita.NotaRecuperacion.HasValue && materiaInscrita.NotaRecuperacion.Value >= 7)
                    {
                        notaFinal = 7;
                    }
                    else if (materiaInscrita.NotaRecuperacion.HasValue)
                    {
                        notaFinal = materiaInscrita.NotaRecuperacion.Value;
                    }
                    else
                    {
                        notaFinal = materiaInscrita.NotaPromedio > 0 ? materiaInscrita.NotaPromedio : totalPuntos;
                    }

                    var notaFinalRedondeada = Math.Round(notaFinal * 10m, 0, MidpointRounding.AwayFromZero) / 10m;

                    tabla.AddCell(CeldaDatosPdf(Math.Round(totalPuntos, 2).ToString("0.00"), fontNormal, 7));
                    tabla.AddCell(CeldaDatosPdf(notaRecuperacion.ToString("0.00"), fontNormal, 7));
                    tabla.AddCell(CeldaDatosPdf(notaFinalRedondeada.ToString("0.00"), fontNormal, 7));
                    tabla.AddCell(CeldaDatosPdf("", fontNormal, 7));
                }

                doc.Add(tabla);

                // Pie de página
                doc.Add(new Paragraph(" ").SetFontSize(4));
                var pieTable = new Table(2).UseAllAvailableWidth();
                
                int aprobadosMasculinos = 0, aprobadosFemeninos = 0, reprobadosMasculinos = 0, reprobadosFemeninos = 0;
                foreach (var mi in estudiantes)
                {
                    var alumno = mi.Alumno;
                    if (alumno == null) continue;
                    bool esMasculino = alumno.Genero == 0;
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

                pieTable.AddCell(new Cell()
                    .Add(new Paragraph($"APROBADOS : MASCULINOS:__{aprobadosMasculinos}___FEMENINOS :__{aprobadosFemeninos}__")
                        .SetFont(fontNormal).SetFontSize(7))
                    .SetBorder(Border.NO_BORDER));
                pieTable.AddCell(new Cell()
                    .Add(new Paragraph($"FECHA DE ENTREGA : {DateTime.Now:dd/MM/yyyy}")
                        .SetFont(fontNormal).SetFontSize(7))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT));

                pieTable.AddCell(new Cell()
                    .Add(new Paragraph($"REPROBADOS : MASCULINOS :__{reprobadosMasculinos}___FEMENINOS :__{reprobadosFemeninos}__")
                        .SetFont(fontNormal).SetFontSize(7))
                    .SetBorder(Border.NO_BORDER));
                pieTable.AddCell(new Cell()
                    .Add(new Paragraph("FIRMA DEL DOCENTE :________________________")
                        .SetFont(fontNormal).SetFontSize(7))
                    .SetBorder(Border.NO_BORDER)
                    .SetTextAlignment(TextAlignment.RIGHT));

                pieTable.AddCell(new Cell()
                    .Add(new Paragraph($"RETIRADOS : MASCULINO :__0__ FEMENINO__0__")
                        .SetFont(fontNormal).SetFontSize(7))
                    .SetBorder(Border.NO_BORDER));
                pieTable.AddCell(new Cell().SetBorder(Border.NO_BORDER).Add(new Paragraph("")));

                doc.Add(pieTable);

                doc.Close();
            }

            return ms.ToArray();
        }

        private Cell CeldaEncabezadoPdf(string texto, PdfFont font, float fontSize)
        {
            return new Cell()
                .Add(new Paragraph(texto).SetFont(font).SetFontSize(fontSize))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetBorder(new SolidBorder(1))
                .SetPadding(3);
        }

        private Cell CeldaDatosPdf(string texto, PdfFont font, float fontSize)
        {
            return new Cell()
                .Add(new Paragraph(texto).SetFont(font).SetFontSize(fontSize))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetBorder(new SolidBorder(1))
                .SetPadding(2);
        }

    }
}
