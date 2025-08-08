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

            var htmlContent = await _pdfService.GenerarHtmlReporteMatriculaSinInscripcion(
                AlumnosConMatriculaSinInscripcion,
                TotalAlumnos,
                TotalMatricula,
                SelectedCarreraId,
                IncluirAlumnosConBeca);

            var pdfBytes = _converter.Convert(new HtmlToPdfDocument()
            {
                GlobalSettings = {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings() { Top = 10, Bottom = 10, Left = 10, Right = 10 }
                },
                Objects = {
                    new ObjectSettings() {
                        HtmlContent = htmlContent
                    }
                }
            });

            return File(pdfBytes, "application/pdf", $"Reporte_Matricula_Sin_Inscripcion_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }

        public async Task<IActionResult> OnGetGenerarExcelAsync()
        {
            await OnGetAsync();

            var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Alumnos con Matrícula Sin Inscripción");

            // Encabezados
            worksheet.Cell("A1").Value = "No.";
            worksheet.Cell("B1").Value = "Nombre Completo";
            worksheet.Cell("C1").Value = "Carrera";
            worksheet.Cell("D1").Value = "Carnet";
            worksheet.Cell("E1").Value = "Email";
            worksheet.Cell("F1").Value = "Fecha Pago Matrícula";
            worksheet.Cell("G1").Value = "Monto Matrícula";
            worksheet.Cell("H1").Value = "Código Generación";

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
                worksheet.Cell($"E{row}").Value = alumno.Email;
                worksheet.Cell($"F{row}").Value = alumno.FechaPagoMatricula.ToString("dd/MM/yyyy");
                worksheet.Cell($"G{row}").Value = alumno.MontoMatricula;
                worksheet.Cell($"H{row}").Value = alumno.CodigoGeneracion;
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
