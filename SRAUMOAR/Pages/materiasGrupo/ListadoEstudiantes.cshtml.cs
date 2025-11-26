using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using ClosedXML.Excel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.materiasGrupo
{
    public class ListadoEstudiantesModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IWebHostEnvironment _environment;
        private static readonly XLColor NotaBackgroundColor = XLColor.FromHtml("#AED99E");

        public ListadoEstudiantesModel(SRAUMOAR.Modelos.Contexto context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public IList<MateriasInscritas> MateriasInscritas { get; set; } = default!;
        public IList<ActividadAcademica> ActividadAcademicas { get; set; } = default!;
        public Grupo Grupo { get; set; } = default!;
        public string NombreMateria { get; set; } = default!;
        public bool lista { get; set; }
        public int idgrupo { get; set; } = default!;

        // Método para calcular el promedio correctamente
        private static decimal CalcularPromedioMateriaComun(ICollection<Notas> notas, IList<ActividadAcademica> actividadesAcademicas)
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

                var notaRegistrada = notas
                    ?.FirstOrDefault(n => n.ActividadAcademicaId == actividad.ActividadAcademicaId);

                decimal valorNota = notaRegistrada?.Nota ?? 0;
                sumaPonderada += valorNota * porcentaje;
            }

            if (totalPorcentaje <= 0) return 0;

            return Math.Round(sumaPonderada / totalPorcentaje, 2);
        }

        public async Task OnGetAsync(int id, bool lista = false)
        {
            this.lista = lista;
            var cicloactual = _context.Ciclos.Where(x => x.Activo == true).First();
            idgrupo = id;
            ActividadAcademicas = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == cicloactual.Id)
                .OrderBy(a => a.FechaInicio)
                .ToListAsync();
            NombreMateria = await ObtenerNombreMateriaAsync(id);
            ViewData["ActividadAcademicaId"] = new SelectList(_context.ActividadesAcademicas
                .Where(a => a.CicloId == cicloactual.Id && a.ActivarIngresoNotas == true)
                .Select(a => new
                {
                    Id = a.ActividadAcademicaId,
                    Descripcion = $"{a.Nombre} - {a.Fecha.ToShortDateString()}"
                }),
                "Id",
                "Descripcion"
            );
            Grupo = await _context.MateriasGrupo
                .Include(g => g.Grupo)
                    .ThenInclude(g => g.Carrera)
                .Include(g => g.Grupo)
                    .ThenInclude(g => g.Docente)
                .Where(mg => mg.MateriasGrupoId == id)
                .Select(mg => mg.Grupo)
                .FirstOrDefaultAsync() ?? new Grupo();

            MateriasInscritas = await _context.MateriasInscritas
                .Include(m => m.Alumno)
                .Include(m => m.MateriasGrupo)
                .Include(m => m.Notas)
                .ThenInclude(m => m.ActividadAcademica)
                .Where(m => m.MateriasGrupoId == id)
                .ToListAsync();

            // Recalcular y actualizar promedios
            bool hayCambios = false;
            foreach (var materia in MateriasInscritas)
            {
                if (ActividadAcademicas != null && ActividadAcademicas.Any())
                {
                    var promedioCalculado = CalcularPromedioMateriaComun(materia.Notas, ActividadAcademicas);

                    if (materia.NotaPromedio != promedioCalculado)
                    {
                        materia.NotaPromedio = promedioCalculado;
                        _context.MateriasInscritas.Update(materia);
                        hayCambios = true;
                    }
                }
            }

            if (hayCambios)
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IActionResult> OnPostGuardarRecuperacionAsync(int materiaInscritaId, decimal notaRecuperacion, int idgrupo)
        {
            var materiaInscrita = await _context.MateriasInscritas
                .FirstOrDefaultAsync(m => m.MateriasInscritasId == materiaInscritaId);

            if (materiaInscrita == null)
            {
                return NotFound("No se encontró la materia inscrita.");
            }

            // Validar que la nota esté en rango válido
            if (notaRecuperacion < 0 || notaRecuperacion > 10)
            {
                TempData["Error"] = "La nota de recuperación debe estar entre 0 y 10.";
                return RedirectToPage(new { id = idgrupo });
            }

            // Guardar la nota de recuperación
            materiaInscrita.NotaRecuperacion = notaRecuperacion;
            materiaInscrita.FechaRecuperacion = DateTime.Now;

            // Si hay nota de recuperación, esa es la nota final para determinar si aprobó
            materiaInscrita.Aprobada = notaRecuperacion >= 7;

            _context.MateriasInscritas.Update(materiaInscrita);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Nota de recuperación guardada correctamente.";
            return RedirectToPage(new { id = idgrupo });
        }

        public async Task<IActionResult> OnGetExportarExcelAsync(int id)
        {
            var cicloactual = await _context.Ciclos.Where(x => x.Activo == true).FirstAsync();

            var materiaGrupo = await _context.MateriasGrupo
                .Include(mg => mg.Materia)
                .Include(mg => mg.Docente)
                .Include(mg => mg.Grupo)
                    .ThenInclude(g => g.Carrera)
                .Include(mg => mg.Grupo)
                    .ThenInclude(g => g.Ciclo)
                .FirstOrDefaultAsync(mg => mg.MateriasGrupoId == id);

            if (materiaGrupo == null)
            {
                return NotFound("No se encontró la materia solicitada.");
            }

            var grupo = materiaGrupo.Grupo;

            var actividades = await _context.ActividadesAcademicas
                .Where(a => a.CicloId == cicloactual.Id)
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
                .Where(mi => mi.MateriasGrupoId == id)
                .OrderBy(mi => mi.Alumno!.Apellidos)
                .ThenBy(mi => mi.Alumno!.Nombres)
                .ToListAsync();

            var archivo = GenerarExcelMateria(grupo, materiaGrupo, actividades, estudiantes);
            var nombreArchivo = $"Notas_{materiaGrupo.Materia?.NombreMateria ?? "Materia"}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(archivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombreArchivo);
        }

        private async Task<string> ObtenerNombreMateriaAsync(int inscripcionMateriaId)
        {
            return await _context.MateriasGrupo
                .Include(im => im.Materia)
                .Where(im => im.MateriasGrupoId == inscripcionMateriaId)
                .Select(im => im.Materia.NombreMateria)
                .FirstOrDefaultAsync();
        }

        private byte[] GenerarExcelMateria(Grupo grupo, MateriasGrupo materiaGrupo, IList<ActividadAcademica> actividades, IList<MateriasInscritas> estudiantes)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Notas");

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

            int labelColumn = 11;

            var rangoAsignatura = worksheet.Range(3, labelColumn, 3, 19);
            rangoAsignatura.Merge();
            rangoAsignatura.Value = $"ASIGNATURA: {materiaGrupo.Materia?.NombreMateria ?? "-"}";
            rangoAsignatura.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            rangoAsignatura.Style.Font.Bold = true;

            var rangoCodigo = worksheet.Range(4, labelColumn, 4, 19);
            rangoCodigo.Merge();
            rangoCodigo.Value = $"CÓDIGO: {materiaGrupo.Materia?.CodigoMateria ?? "-"}";
            rangoCodigo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            var rangoDia = worksheet.Range(5, labelColumn, 5, 19);
            rangoDia.Merge();
            rangoDia.Value = $"DÍA: {materiaGrupo.Dia.GetDisplayName()}";
            rangoDia.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            var rangoHorario = worksheet.Range(6, labelColumn, 6, 19);
            rangoHorario.Merge();
            rangoHorario.Value = $"HORARIO: {materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraInicio)} a {materiaGrupo.FormatearHora12Horas(materiaGrupo.HoraFin)}";
            rangoHorario.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

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

            worksheet.Column(1).Width = 4;
            worksheet.Column(2).Width = 10;
            worksheet.Column(3).Width = 35;
            worksheet.Column(4).Width = 6;
            worksheet.Column(5).Width = 6;
            worksheet.Column(6).Width = 6;
            worksheet.Column(7).Width = 6;
            worksheet.Column(8).Width = 6;
            worksheet.Column(9).Width = 6;
            worksheet.Column(10).Width = 6;
            worksheet.Column(11).Width = 6;
            worksheet.Column(12).Width = 6;
            worksheet.Column(13).Width = 6;
            worksheet.Column(14).Width = 6;
            worksheet.Column(15).Width = 6;
            worksheet.Column(16).Width = 10;
            worksheet.Column(17).Width = 12;
            worksheet.Column(18).Width = 8;
            worksheet.Column(19).Width = 14;
        }

        private void InsertarLogo(IXLWorksheet worksheet)
        {
            try
            {
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
                Console.WriteLine($"Error al insertar logo: {ex.Message}");
            }
        }

        private void InsertarPieDePagina(IXLWorksheet worksheet, int filaInicio, IList<MateriasInscritas> estudiantes)
        {
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

                bool esMasculino = alumno.Genero == 0;
                // Si hay nota de recuperación, esa determina si aprobó
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

            worksheet.Cell(filaActual, 1).Value = $"APROBADOS : MASCULINOS:__{aprobadosMasculinos}___FEMENINOS :__{aprobadosFemeninos}__";
            worksheet.Range(filaActual, 1, filaActual, 8).Merge();

            worksheet.Cell(filaActual, 12).Value = $"FECHA DE ENTREGA : {DateTime.Now:dd/MM/yyyy}";
            worksheet.Range(filaActual, 12, filaActual, 19).Merge();

            filaActual++;

            worksheet.Cell(filaActual, 1).Value = $"REPROBADOS : MASCULINOS :__{reprobadosMasculinos}___FEMENINOS :__{reprobadosFemeninos}__";
            worksheet.Range(filaActual, 1, filaActual, 8).Merge();

            worksheet.Cell(filaActual, 12).Value = "FIRMA DEL DOCENTE :________________________";
            worksheet.Range(filaActual, 12, filaActual, 19).Merge();

            filaActual++;

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
    }
}
