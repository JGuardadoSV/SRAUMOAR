using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Modelos;
using DinkToPdf;
using DinkToPdf.Contracts;
using SRAUMOAR.Servicios;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

namespace SRAUMOAR.Pages.reportes.matricula_sin_inscripcion
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IConverter _converter;
        private readonly PdfService _pdfService;

        public IndexModel(
            SRAUMOAR.Modelos.Contexto context,
            IConverter converter,
            PdfService pdfService)
        {
            _context = context;
            _converter = converter;
            _pdfService = pdfService;
        }

        [BindProperty(SupportsGet = true)]
        public int? SelectedCarreraId { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IncluirAlumnosConBeca { get; set; } = false;

        public List<AlumnoConMatricula> AlumnosConMatriculaSinInscripcion { get; set; } = new List<AlumnoConMatricula>();
        public int TotalAlumnos { get; set; }
        public decimal TotalMatricula { get; set; }

        public class AlumnoConMatricula
        {
            public int AlumnoId { get; set; }
            public string Nombres { get; set; } = "";
            public string Apellidos { get; set; } = "";
            public string Carnet { get; set; } = "";
            public string Email { get; set; } = "";
            public string Carrera { get; set; } = "";
            public DateTime FechaPagoMatricula { get; set; }
            public decimal MontoMatricula { get; set; }
            public string CodigoGeneracion { get; set; } = "";
        }

        public async Task OnGetAsync()
        {
            var cicloActual = await _context.Ciclos
                .Where(x => x.Activo == true)
                .FirstOrDefaultAsync();

            if (cicloActual == null)
            {
                return;
            }

            // Obtener alumnos que pagaron matrícula en el ciclo actual
            var alumnosConMatricula = await _context.CobrosArancel
                .Include(c => c.Alumno)
                    .ThenInclude(a => a.Carrera)
                .Include(c => c.DetallesCobroArancel)
                    .ThenInclude(d => d.Arancel)
                .Where(c => c.CicloId == cicloActual.Id &&
                           c.DetallesCobroArancel.Any(d => d.Arancel.Nombre.ToLower().Contains("matricula")))
                .ToListAsync();

            // Obtener alumnos inscritos en el ciclo actual
            var alumnosInscritos = await _context.Inscripciones
                .Where(i => i.CicloId == cicloActual.Id)
                .Select(i => i.AlumnoId)
                .ToListAsync();

            // Filtrar alumnos que pagaron matrícula pero no están inscritos
            var alumnosConMatriculaSinInscripcion = alumnosConMatricula
                .Where(c => !alumnosInscritos.Contains(c.AlumnoId.Value))
                .ToList();

            // Aplicar filtro de carrera si se especifica
            if (SelectedCarreraId.HasValue && SelectedCarreraId.Value > 0)
            {
                alumnosConMatriculaSinInscripcion = alumnosConMatriculaSinInscripcion
                    .Where(c => c.Alumno.CarreraId == SelectedCarreraId.Value)
                    .ToList();
            }

            // Aplicar filtro de becas si no se incluyen
            if (!IncluirAlumnosConBeca)
            {
                var alumnosConBeca = await _context.Becados
                    .Where(b => b.Estado)
                    .Select(b => b.AlumnoId)
                    .ToListAsync();

                alumnosConMatriculaSinInscripcion = alumnosConMatriculaSinInscripcion
                    .Where(c => !alumnosConBeca.Contains(c.AlumnoId.Value))
                    .ToList();
            }

            // Convertir a la clase de resultado
            AlumnosConMatriculaSinInscripcion = alumnosConMatriculaSinInscripcion
                .Select(c => new AlumnoConMatricula
                {
                    AlumnoId = c.AlumnoId.Value,
                    Nombres = c.Alumno?.Nombres ?? "",
                    Apellidos = c.Alumno?.Apellidos ?? "",
                    Carnet = c.Alumno?.Carnet ?? "",
                    Email = c.Alumno?.Email ?? "",
                    Carrera = c.Alumno?.Carrera?.NombreCarrera ?? "",
                    FechaPagoMatricula = c.Fecha,
                    MontoMatricula = c.DetallesCobroArancel
                        .Where(d => d.Arancel.Nombre.ToLower().Contains("matricula"))
                        .Sum(d => d.costo),
                    CodigoGeneracion = c.CodigoGeneracion ?? ""
                })
                .OrderBy(a => a.Apellidos)
                .ThenBy(a => a.Nombres)
                .ToList();

            TotalAlumnos = AlumnosConMatriculaSinInscripcion.Count;
            TotalMatricula = AlumnosConMatriculaSinInscripcion.Sum(a => a.MontoMatricula);

            // Cargar datos para los filtros
            ViewData["Carreras"] = new SelectList(
                _context.Carreras
                    .OrderBy(c => c.NombreCarrera)
                    .Select(c => new { Id = c.CarreraId, Nombre = c.NombreCarrera }),
                "Id", "Nombre", SelectedCarreraId);
        }

        public async Task<IActionResult> OnGetGenerarPDFAsync()
        {
            await OnGetAsync();

            if (!AlumnosConMatriculaSinInscripcion.Any())
            {
                TempData["Error"] = "No hay datos para generar el PDF";
                return RedirectToPage();
            }

            using var memoryStream = new MemoryStream();
            var writer = new iText.Kernel.Pdf.PdfWriter(memoryStream);
            var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
            var document = new iText.Layout.Document(pdf, iText.Kernel.Geom.PageSize.A4);

            // Configurar fuentes que soporten caracteres especiales (Ñ, acentos, etc.)
            var fontNormal = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.TIMES_ROMAN);
            var fontBold = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.TIMES_BOLD);
            var fontItalic = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.TIMES_ITALIC);

            // Color para texto (negro estándar)
            var colorTexto = iText.Kernel.Colors.ColorConstants.BLACK;

            // Configurar márgenes
            document.SetMargins(40, 40, 40, 40);

            // ========== ENCABEZADO LIMPIO ==========

            // Título principal
            var tituloUniversidad = new iText.Layout.Element.Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                .SetFontSize(18)
                .SetFont(fontBold)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                .SetFontColor(colorTexto)
                .SetMarginBottom(8);
            document.Add(tituloUniversidad);

            // Subtítulo del reporte
            var subtituloReporte = new iText.Layout.Element.Paragraph("REPORTE DE ALUMNOS CON MATRÍCULA SIN INSCRIPCIÓN")
                .SetFontSize(14)
                .SetFont(fontBold)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                .SetFontColor(colorTexto)
                .SetMarginBottom(15);
            document.Add(subtituloReporte);

            // ========== INFORMACIÓN DEL REPORTE ==========

            // Fecha de generación
            var fechaInfo = new iText.Layout.Element.Paragraph($"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}")
                .SetFontSize(10)
                .SetFont(fontNormal)
                .SetFontColor(colorTexto)
                .SetMarginBottom(5);
            document.Add(fechaInfo);

            // Filtro de carrera (si aplica)
            if (SelectedCarreraId.HasValue)
            {
                var carrera = _context.Carreras.FirstOrDefault(c => c.CarreraId == SelectedCarreraId.Value);
                var filtroCarrera = new iText.Layout.Element.Paragraph($"Filtro aplicado - Carrera: {carrera?.NombreCarrera ?? "N/A"}")
                    .SetFontSize(10)
                    .SetFont(fontNormal)
                    .SetFontColor(colorTexto)
                    .SetMarginBottom(5);
                document.Add(filtroCarrera);
            }

            // Filtro de beca
            var filtroBeca = new iText.Layout.Element.Paragraph($"Incluir alumnos con beca: {(IncluirAlumnosConBeca ? "Sí" : "No")}")
                .SetFontSize(10)
                .SetFont(fontNormal)
                .SetFontColor(colorTexto)
                .SetMarginBottom(10);
            document.Add(filtroBeca);

            // ========== RESUMEN ==========

            var resumenText = new iText.Layout.Element.Paragraph($"Total de alumnos encontrados: {TotalAlumnos}")
                .SetFontSize(12)
                .SetFont(fontBold)
                .SetFontColor(colorTexto)
                .SetMarginBottom(15);
            document.Add(resumenText);

            // ========== TABLA DE DATOS COMPACTA ==========

            var table = new iText.Layout.Element.Table(new float[] { 1f, 4f, 3f, 2f }).UseAllAvailableWidth();

            // Encabezados de tabla
            string[] headers = { "No.", "Nombre Completo", "Carrera", "Carnet" };
            foreach (string header in headers)
            {
                var headerCell2 = new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(header)
                        .SetFontSize(12)
                        .SetFont(fontBold)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER))
                    .SetFontColor(colorTexto)
                    .SetPadding(6)
                    .SetBorder(new iText.Layout.Borders.SolidBorder(colorTexto, 1));

                table.AddHeaderCell(headerCell2);
            }

            // Datos de la tabla compactos
            int contador = 1;
            foreach (var alumno in AlumnosConMatriculaSinInscripcion)
            {
                // Número
                var numeroCell = new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(contador.ToString())
                        .SetFontSize(11)
                        .SetFont(fontNormal)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER))
                    .SetPadding(4)
                    .SetBorder(new iText.Layout.Borders.SolidBorder(colorTexto, 1))
                    .SetFontColor(colorTexto);
                table.AddCell(numeroCell);

                // Nombre completo
                var nombreCell = new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph($"{alumno.Apellidos}, {alumno.Nombres}")
                        .SetFontSize(11)
                        .SetFont(fontNormal))
                    .SetPadding(4)
                    .SetBorder(new iText.Layout.Borders.SolidBorder(colorTexto, 1))
                    .SetFontColor(colorTexto);
                table.AddCell(nombreCell);

                // Carrera
                var carreraCell = new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(alumno.Carrera ?? "")
                        .SetFontSize(11)
                        .SetFont(fontNormal))
                    .SetPadding(4)
                    .SetBorder(new iText.Layout.Borders.SolidBorder(colorTexto, 1))
                    .SetFontColor(colorTexto);
                table.AddCell(carreraCell);

                // Carnet
                var carnetText = alumno.Carnet ?? alumno.Email?.Split('@')[0] ?? "";
                var carnetCell = new iText.Layout.Element.Cell()
                    .Add(new iText.Layout.Element.Paragraph(carnetText)
                        .SetFontSize(11)
                        .SetFont(fontNormal)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER))
                    .SetPadding(4)
                    .SetBorder(new iText.Layout.Borders.SolidBorder(colorTexto, 1))
                    .SetFontColor(colorTexto);
                table.AddCell(carnetCell);

                contador++;
            }

            document.Add(table);

            // ========== PIE DE PÁGINA SIMPLE ==========

            document.Add(new iText.Layout.Element.Paragraph("\n").SetMarginBottom(10));

            var footerText = new iText.Layout.Element.Paragraph("Generado por el Sistema Académico UMOAR")
                .SetFontSize(9)
                .SetFont(fontItalic)
                .SetFontColor(colorTexto)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER);
            document.Add(footerText);

            document.Close();

            var fileName = $"Reporte_Matricula_Sin_Inscripcion_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";

            return File(memoryStream.ToArray(), "application/pdf");
        }

        public async Task<IActionResult> OnGetGenerarExcelAsync()
        {
            await OnGetAsync();

            var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Matricula Sin Inscripcion");

            // Encabezados
            worksheet.Cell("A1").Value = "No.";
            worksheet.Cell("B1").Value = "Nombre Completo";
            worksheet.Cell("C1").Value = "Carrera";
            worksheet.Cell("D1").Value = "Carnet";
           // worksheet.Cell("E1").Value = "Email";
            worksheet.Cell("E1").Value = "Fecha Pago Matrícula";
            //worksheet.Cell("G1").Value = "Monto Matrícula";
            //worksheet.Cell("H1").Value = "Código Generación";

            // Aplicar formato a encabezados
            var headerRange = worksheet.Range("A1:H1");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

            // Datos
            for (int i = 0; i < AlumnosConMatriculaSinInscripcion.Count; i++)
            {
                var alumno = AlumnosConMatriculaSinInscripcion[i];
                var row = i + 2;

                worksheet.Cell($"A{row}").Value = i + 1;
                worksheet.Cell($"B{row}").Value = $"{alumno.Apellidos}, {alumno.Nombres}";
                worksheet.Cell($"C{row}").Value = alumno.Carrera;
                worksheet.Cell($"D{row}").Value = alumno.Carnet;
                //worksheet.Cell($"E{row}").Value = alumno.Email;
                worksheet.Cell($"E{row}").Value = alumno.FechaPagoMatricula.ToString("dd/MM/yyyy");
                //worksheet.Cell($"G{row}").Value = alumno.MontoMatricula;
                //worksheet.Cell($"H{row}").Value = alumno.CodigoGeneracion;
            }

            // Autoajustar columnas
            worksheet.Columns().AdjustToContents();

            // Agregar resumen
            var summaryRow = AlumnosConMatriculaSinInscripcion.Count + 3;
            worksheet.Cell($"A{summaryRow}").Value = "RESUMEN";
            worksheet.Cell($"A{summaryRow}").Style.Font.Bold = true;

            worksheet.Cell($"A{summaryRow + 1}").Value = "Total de alumnos:";
            worksheet.Cell($"B{summaryRow + 1}").Value = TotalAlumnos;
            worksheet.Cell($"A{summaryRow + 2}").Value = "Total matrícula:";
            worksheet.Cell($"B{summaryRow + 2}").Value = TotalMatricula;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"Reporte_Matricula_Sin_Inscripcion_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
        }
    }
}
