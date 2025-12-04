using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Layout.Borders;
using iText.Kernel.Colors;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using System.Text.RegularExpressions;
using System.Web;

namespace SRAUMOAR.Pages.reportes.notas
{
    public class reporteNotasParcialesModel : PageModel
    {
        private readonly Contexto _context;

        private PdfFont _fontNormal;
        private PdfFont _fontBold;

        public reporteNotasParcialesModel(Contexto context)
        {
            _context = context;
        }

        /// <summary>
        /// Calcula el promedio ponderado de notas usando los porcentajes configurados en las actividades académicas
        /// </summary>
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
        private static decimal CalcularNotaFinal(MateriasInscritas materiaInscrita, ICollection<Notas> notasMateria, IList<ActividadAcademica> actividadesAcademicas)
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

        private void InitializeFonts()
        {
            _fontNormal = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
            _fontBold = PdfFontFactory.CreateFont(StandardFonts.TIMES_BOLD);
        }

        public IActionResult OnGet(int alumnoId)
        {
            InitializeFonts();

            var cicloActual = _context.Ciclos.FirstOrDefault(c => c.Activo);
            if (cicloActual == null)
            {
                return NotFound("No hay ciclo activo");
            }

            var alumno = _context.Alumno
                .Include(a => a.Carrera)
                    .ThenInclude(c => c!.Facultad)
                .FirstOrDefault(a => a.AlumnoId == alumnoId);

            if (alumno == null)
            {
                return NotFound();
            }

            var materiasInscritas = _context.MateriasInscritas
                .Include(mi => mi.MateriasGrupo)!.ThenInclude(mg => mg!.Materia)
                .Include(mi => mi.Notas)!.ThenInclude(n => n!.ActividadAcademica)
                .Where(mi => mi.AlumnoId == alumnoId && mi.MateriasGrupo!.Grupo!.CicloId == cicloActual.Id)
                .ToList();

            // Obtener actividades académicas del ciclo para calcular promedios correctamente
            var actividadesAcademicas = _context.ActividadesAcademicas
                .Where(a => a.CicloId == cicloActual.Id)
                .OrderBy(a => a.FechaInicio)
                .ToList();

            // Determinar si todas las actividades tienen notas (para cambiar el título del reporte)
            bool todasLasNotasCompletas = true;
            if (actividadesAcademicas.Any() && materiasInscritas.Any())
            {
                foreach (var mi in materiasInscritas)
                {
                    var notasMateria = (mi.Notas ?? new List<Notas>())
                        .Where(n => n.ActividadAcademica != null)
                        .ToList();
                    
                    // Verificar si tiene nota para cada actividad académica
                    foreach (var actividad in actividadesAcademicas)
                    {
                        if (!notasMateria.Any(n => n.ActividadAcademicaId == actividad.ActividadAcademicaId))
                        {
                            todasLasNotasCompletas = false;
                            break;
                        }
                    }
                    if (!todasLasNotasCompletas) break;
                }
            }
            else
            {
                todasLasNotasCompletas = false;
            }

            // Título dinámico del reporte
            string tituloReporte = todasLasNotasCompletas 
                ? $"Reporte de notas ciclo {cicloActual.NCiclo} - {cicloActual.anio}"
                : $"Informe de notas parciales ciclo {cicloActual.NCiclo} - {cicloActual.anio}";

            // Determinar estado de solvencia
            var hoy = DateTime.Now.Date;
            var esBecado = _context.Becados.Any(b => b.AlumnoId == alumnoId && b.CicloId == cicloActual.Id && b.Estado);
            var arancelesObligatoriosVigentes = _context.Aranceles
                .Where(a => a.CicloId == cicloActual.Id && a.Obligatorio && a.Activo && a.FechaInicio <= hoy)
                .ToList();
            var arancelesPagadosIds = _context.DetallesCobrosArancel
                .Where(d => d.CobroArancel.AlumnoId == alumnoId && d.CobroArancel.CicloId == cicloActual.Id)
                .Select(d => d.ArancelId)
                .ToHashSet();
            bool tienePendientes = arancelesObligatoriosVigentes.Any(a => !arancelesPagadosIds.Contains(a.ArancelId));
            string estadoSolvencia = esBecado ? "Solvente (Becado)" : (tienePendientes ? "No solvente" : "Solvente");

            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            {
                using var pdf = new PdfDocument(writer);
                var doc = new Document(pdf, iText.Kernel.Geom.PageSize.LETTER);
                doc.SetMargins(36, 36, 36, 36);

                // Encabezado con logo + textos centrados
                var headerTable = new Table(2).UseAllAvailableWidth();
                try
                {
                    var logoPath = Path.Combine("wwwroot", "images", "logoUmoar.jpg");
                    var logo = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(logoPath)).SetWidth(60);
                    headerTable.AddCell(new Cell().Add(logo).SetBorder(Border.NO_BORDER).SetVerticalAlignment(iText.Layout.Properties.VerticalAlignment.MIDDLE));
                }
                catch
                {
                    headerTable.AddCell(new Cell().Add(new Paragraph(" ")).SetBorder(Border.NO_BORDER));
                }
                var textCell = new Cell().SetBorder(Border.NO_BORDER);
                textCell.Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO").SetFont(_fontBold).SetFontSize(14).SetTextAlignment(TextAlignment.CENTER));
                textCell.Add(new Paragraph(tituloReporte).SetFont(_fontBold).SetFontSize(12).SetTextAlignment(TextAlignment.CENTER));
                headerTable.AddCell(textCell);
                doc.Add(headerTable);
                // Línea separadora sutil
                var separator = new Paragraph(" ")
                    .SetBorderBottom(new SolidBorder(ColorConstants.LIGHT_GRAY, 1))
                    .SetMarginTop(4)
                    .SetMarginBottom(12);
                doc.Add(separator);

                // Datos del alumno
                string carnet = (alumno.Email ?? string.Empty).Split('@').FirstOrDefault() ?? string.Empty;
                var datos = new Table(new float[] { 120, 10, 400 }).UseAllAvailableWidth();
                datos.AddCell(CeldaLabel("Nombre"));
                datos.AddCell(CeldaSeparador());
                datos.AddCell(CeldaValor($"{alumno.Apellidos} {alumno.Nombres}"));
                datos.AddCell(CeldaLabel("Carnet"));
                datos.AddCell(CeldaSeparador());
                datos.AddCell(CeldaValor(carnet));
                datos.AddCell(CeldaLabel("Facultad"));
                datos.AddCell(CeldaSeparador());
                datos.AddCell(CeldaValor(alumno.Carrera?.Facultad?.NombreFacultad ?? ""));
                datos.AddCell(CeldaLabel("Carrera"));
                datos.AddCell(CeldaSeparador());
                datos.AddCell(CeldaValor(alumno.Carrera?.NombreCarrera ?? ""));
                datos.AddCell(CeldaLabel("Estado"));
                datos.AddCell(CeldaSeparador());
                datos.AddCell(CeldaValor(estadoSolvencia));
                doc.Add(datos);

                doc.Add(new Paragraph(" "));

                // Tabla de notas: COD, Materia, L1 P1 L2 P2 L3 P3, Nota final
                var tabla = new Table(new float[] { 60, 220, 45, 45, 45, 45, 45, 45, 70 }).UseAllAvailableWidth();
                EstiloEncabezado(tabla, "COD");
                EstiloEncabezado(tabla, "Materia");
                EstiloEncabezado(tabla, "LAB1");
                EstiloEncabezado(tabla, "PAR1");
                EstiloEncabezado(tabla, "LAB2");
                EstiloEncabezado(tabla, "PAR2");
                EstiloEncabezado(tabla, "LAB3");
                EstiloEncabezado(tabla, "PAR3");
                EstiloEncabezado(tabla, "Nota final");

                int rowIndex = 0;
                foreach (var mi in materiasInscritas.OrderBy(x => x.MateriasGrupo!.Materia!.NombreMateria))
                {
                    var materia = mi.MateriasGrupo!.Materia!;
                    var notas = (mi.Notas ?? new List<Notas>())
                        .Where(n => n.ActividadAcademica != null)
                        .ToList();

                    var labs = notas.Where(n => n.ActividadAcademica!.TipoActividad == 1)
                                    .OrderBy(n => n.ActividadAcademica!.FechaInicio)
                                    .Take(3)
                                    .Select(n => (decimal?)n.Nota)
                                    .ToList();
                    var pars = notas.Where(n => n.ActividadAcademica!.TipoActividad == 2)
                                    .OrderBy(n => n.ActividadAcademica!.FechaInicio)
                                    .Take(3)
                                    .Select(n => (decimal?)n.Nota)
                                    .ToList();

                    while (labs.Count < 3) labs.Add(null);
                    while (pars.Count < 3) pars.Add(null);

                    // Celdas de datos (con zebra striping)
                    bool isAlt = (rowIndex % 2 == 1);
                    tabla.AddCell(CeldaTexto(materia.CodigoMateria ?? "", isAlt));
                    tabla.AddCell(CeldaTexto(materia.NombreMateria ?? "", isAlt));
                    tabla.AddCell(CeldaNota(labs[0], isAlt));
                    tabla.AddCell(CeldaNota(pars[0], isAlt));
                    tabla.AddCell(CeldaNota(labs[1], isAlt));
                    tabla.AddCell(CeldaNota(pars[1], isAlt));
                    tabla.AddCell(CeldaNota(labs[2], isAlt));
                    tabla.AddCell(CeldaNota(pars[2], isAlt));

                    // Calcular nota final usando porcentajes configurados y regla de reposición
                    decimal notaFinal = CalcularNotaFinal(mi, notas, actividadesAcademicas);
                    tabla.AddCell(CeldaNota(notaFinal, isAlt));

                    rowIndex++;
                }

                doc.Add(tabla);

                doc.Add(new Paragraph(" "));
                var firmaTabla = new Table(new float[] { 1, 1 }).UseAllAvailableWidth();
                firmaTabla.AddCell(new Cell().Add(new Paragraph("FIRMA").SetFont(_fontBold)).SetBorderTop(new SolidBorder(1)).SetBorderLeft(Border.NO_BORDER).SetBorderRight(Border.NO_BORDER).SetBorderBottom(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER));
                firmaTabla.AddCell(new Cell().Add(new Paragraph("SELLO").SetFont(_fontBold)).SetBorderTop(new SolidBorder(1)).SetBorderLeft(Border.NO_BORDER).SetBorderRight(Border.NO_BORDER).SetBorderBottom(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER));
                doc.Add(firmaTabla);

                doc.Close();
            }

            // Crear nombre de archivo sin caracteres especiales para evitar errores en headers HTTP
            var apellidosLimpios = Regex.Replace(alumno.Apellidos ?? "", @"[^\w\s-]", "").Replace(" ", "_");
            var nombresLimpios = Regex.Replace(alumno.Nombres ?? "", @"[^\w\s-]", "").Replace(" ", "_");
            var fileName = $"NotasParciales_{apellidosLimpios}_{nombresLimpios}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            
            // Codificar el nombre del archivo para headers HTTP usando RFC 5987
            var encodedFileName = System.Web.HttpUtility.UrlEncode(fileName);
            Response.Headers["Content-Disposition"] = $"inline; filename*=UTF-8''{encodedFileName}";
            return File(ms.ToArray(), "application/pdf");
        }

        private Cell CeldaLabel(string text)
        {
            return new Cell().Add(new Paragraph(text).SetFont(_fontBold)).SetBorder(Border.NO_BORDER);
        }
        private Cell CeldaSeparador()
        {
            return new Cell().Add(new Paragraph(":")).SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.CENTER);
        }
        private Cell CeldaValor(string text)
        {
            return new Cell().Add(new Paragraph(text).SetFont(_fontNormal)).SetBorder(Border.NO_BORDER);
        }
        private void EstiloEncabezado(Table tabla, string texto)
        {
            tabla.AddCell(new Cell()
                .Add(new Paragraph(texto).SetFont(_fontBold))
                .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetBorder(new SolidBorder(1))
                .SetPadding(6));
        }
        private Cell CeldaTexto(string text, bool alt = false)
        {
            var cell = new Cell().Add(new Paragraph(text).SetFont(_fontNormal)).SetBorder(new SolidBorder(1)).SetPadding(5);
            if (alt) cell.SetBackgroundColor(new DeviceRgb(248, 248, 248));
            return cell;
        }
        private Cell CeldaNota(decimal? valor, bool alt = false)
        {
            var texto = valor.HasValue ? valor.Value.ToString("0.00") : "0.00";
            var cell = new Cell().Add(new Paragraph(texto).SetTextAlignment(TextAlignment.CENTER).SetFont(_fontNormal)).SetBorder(new SolidBorder(1)).SetPadding(5);
            if (alt) cell.SetBackgroundColor(new DeviceRgb(248, 248, 248));
            return cell;
        }
    }
}


