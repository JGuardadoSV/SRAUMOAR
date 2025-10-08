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
                textCell.Add(new Paragraph($"Informe de notas parciales ciclo {cicloActual.NCiclo} - {cicloActual.anio}").SetFont(_fontBold).SetFontSize(12).SetTextAlignment(TextAlignment.CENTER));
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

                    // Promedio final: (promedio LAB * 0.30) + (promedio PAR * 0.70)
                    decimal labSum = (labs[0] ?? 0) + (labs[1] ?? 0) + (labs[2] ?? 0);
                    decimal parSum = (pars[0] ?? 0) + (pars[1] ?? 0) + (pars[2] ?? 0);
                    decimal labAvg = labSum / 3m;
                    decimal parAvg = parSum / 3m;
                    decimal notaFinal = Math.Round(labAvg * 0.30m + parAvg * 0.70m, 2);
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

            var fileName = $"NotasParciales_{alumno.Apellidos}_{alumno.Nombres}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            Response.Headers["Content-Disposition"] = $"inline; filename={fileName}";
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


