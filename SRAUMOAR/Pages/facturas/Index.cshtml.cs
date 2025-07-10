using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using Newtonsoft.Json;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using iText.Layout;
using SRAUMOAR.Entidades.Alumnos;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using System.IO;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Layout.Borders;
using System.Globalization;

namespace SRAUMOAR.Pages.facturas
{
    public class IndexModel : PageModel
    {
        private readonly SRAUMOAR.Modelos.Contexto _context;
        private readonly IHttpClientFactory _httpClientFactory;
        public IndexModel(SRAUMOAR.Modelos.Contexto context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            
        }

        public IList<Factura> Factura { get;set; } = default!;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public string? EstadoFiltro { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PaginaActual { get; set; } = 1;

        public int TotalPaginas { get; set; }
        public int TotalRegistros { get; set; }
        public int RegistrosPorPagina { get; set; } = 20;

        public async Task OnGetAsync()
        {
            var query = _context.Facturas.AsQueryable();

            // Filtro por fechas - por defecto muestra solo las del día actual
            if (FechaInicio.HasValue)
            {
                query = query.Where(f => f.Fecha >= FechaInicio.Value);
            }

            if (FechaFin.HasValue)
            {
                query = query.Where(f => f.Fecha <= FechaFin.Value.AddDays(1).AddSeconds(-1));
            }

            // Filtro por estado
            if (!string.IsNullOrEmpty(EstadoFiltro))
            {
                switch (EstadoFiltro.ToLower())
                {
                    case "activa":
                        query = query.Where(f => !f.Anulada);
                        break;
                    case "anulada":
                        query = query.Where(f => f.Anulada);
                        break;
                }
            }

            // Calcular total de registros
            TotalRegistros = await query.CountAsync();
            TotalPaginas = (int)Math.Ceiling((double)TotalRegistros / RegistrosPorPagina);

            // Aplicar paginación
            var facturasPaginadas = await query
                .OrderByDescending(f => f.Fecha)
                .Skip((PaginaActual - 1) * RegistrosPorPagina)
                .Take(RegistrosPorPagina)
                .ToListAsync();

            Factura = facturasPaginadas;
        }

        // Método para obtener todas las facturas filtradas (sin paginación)
        private async Task<List<Factura>> ObtenerFacturasFiltradasAsync()
        {
            var query = _context.Facturas.AsQueryable();

            if (FechaInicio.HasValue)
            {
                query = query.Where(f => f.Fecha >= FechaInicio.Value);
            }

            if (FechaFin.HasValue)
            {
                query = query.Where(f => f.Fecha <= FechaFin.Value.AddDays(1).AddSeconds(-1));
            }

            if (!string.IsNullOrEmpty(EstadoFiltro))
            {
                switch (EstadoFiltro.ToLower())
                {
                    case "activa":
                        query = query.Where(f => !f.Anulada);
                        break;
                    case "anulada":
                        query = query.Where(f => f.Anulada);
                        break;
                }
            }

            return await query.OrderByDescending(f => f.Fecha).ToListAsync();
        }

        // Método para obtener el texto del tipo DTE
        private string ObtenerTipoDTETexto(int tipoDTE)
        {
            return tipoDTE switch
            {
                1 => "CF",
                3 => "CCF",
                5 => "NC",
                14 => "SE",
                15 => "DON",
                _ => tipoDTE.ToString()
            };
        }

        public IActionResult OnGetGenerarPDFSinDatos()
        {
            using var memoryStream = new MemoryStream();
            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            document.Add(new Paragraph("Aviso")
                .SetFontSize(18)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            document.Add(new Paragraph("No hay datos disponibles para generar el PDF.")
                .SetFontSize(12)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            document.Close();
            Response.Headers["Content-Disposition"] = "inline; filename=SinDatos.pdf";
            return File(memoryStream.ToArray(), "application/pdf");

        }

        public async Task<IActionResult> OnGetGenerarPdfAsync(int id)
        {
            try
            {
                
                
                Factura factura1 = await _context.Facturas
                    .FirstOrDefaultAsync(c => c.FacturaId == id);
                Factura factura = await _context.Facturas.FirstOrDefaultAsync(f => f.CodigoGeneracion == factura1.CodigoGeneracion);
                if (string.IsNullOrWhiteSpace(factura?.JsonDte))
                {

                    return OnGetGenerarPDFSinDatos();
                }

                CobroArancel cobroArancel = await _context.CobrosArancel
                 .Include(c => c.Alumno).ThenInclude(a => a.Carrera)
                 .Include(c => c.Ciclo)
                 .FirstOrDefaultAsync(c => c.CodigoGeneracion == factura1.CodigoGeneracion);

                bool existeCobro = cobroArancel != null;
                Alumno alumno=new Alumno();
                if (existeCobro)
                {
                     alumno = cobroArancel.Alumno;
                    // Continúa con tu lógica...
                }
                else
                {
                    var jsonObj = JObject.Parse(factura.JsonDte); // OBTENER EL JSON ORIGINAL PARA LEERLO EN CADA SECCION
                                                                  // Sección de Emisor
                    string correo = jsonObj?["receptor"]?["correo"]?.ToString() ?? "";

                    alumno = _context.Alumno.FirstOrDefault(a => a.Email.Equals(correo));
                }

                var dteJson = factura.JsonDte; // Reemplazar con tu lógica
                var selloRecibido = factura.SelloRecepcion; // Reemplazar con tu lógica
                var tipo = factura.TipoDTE.ToString().PadLeft(2, '0');

                // Datos que necesitas enviar
                var requestData = new
                {
                    dteJson = dteJson,
                    selloRecibido = selloRecibido,
                    tipoDte = tipo,
                    carrera = alumno?.Carrera?.NombreCarrera ?? "-",
                    observacion = cobroArancel?.nota??"-"
                };

                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                //var response = await client.PostAsync("https://localhost:7122/api/generar-pdf", content);
                var response = await client.PostAsync("http://207.58.153.147:7122/api/generar-pdf", content);

                if (response.IsSuccessStatusCode)
                {
                    var pdfBytes = await response.Content.ReadAsByteArrayAsync();

                    //Console.WriteLine($"[DEBUG CLIENT] PDF recibido, tamaño: {pdfBytes.Length} bytes");

                    // Respuesta más simple - deja que la API maneje los headers
                    return File(pdfBytes, "application/pdf");
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[ERROR CLIENT] Error de API: {errorMessage}");
                    TempData["Error"] = $"Error al generar PDF: {errorMessage}";
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR CLIENT] Excepción: {ex}");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Método para generar reporte PDF de todas las facturas
        public async Task<IActionResult> OnGetGenerarReportePDFAsync()
        {
            try
            {
                var facturas = await ObtenerFacturasFiltradasAsync();
                
                if (!facturas.Any())
                {
                    return OnGetGenerarPDFSinDatos();
                }

                using var memoryStream = new MemoryStream();
                var writer = new PdfWriter(memoryStream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                // Configurar el documento
                document.SetMargins(20, 20, 20, 20);

                // Título del reporte
                var titulo = new Paragraph("REPORTE DE FACTURAS")
                    .SetFontSize(20)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(20);
                document.Add(titulo);

                // Información del filtro
                var filtroInfo = new Paragraph($"Período: {FechaInicio?.ToString("dd/MM/yyyy")} - {FechaFin?.ToString("dd/MM/yyyy")}")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(20);
                document.Add(filtroInfo);

                // Crear tabla
                var table = new Table(12).UseAllAvailableWidth();
                
                // Configurar encabezados
                var headers = new[] { 
                    "ID", "Estado", "Fecha", "Tipo DTE", "Código Generación", 
                    "Número Control", "Sello Recepción", "Sello Anulación",
                    "Total Gravado", "Total Exento", "IVA", "Total a Pagar"
                };

                // Agregar encabezados
                foreach (var header in headers)
                {
                    var cell = new Cell()
                        .Add(new Paragraph(header))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetBorder(Border.NO_BORDER);
                    table.AddHeaderCell(cell);
                }

                // Agregar datos
                foreach (var factura in facturas)
                {
                    table.AddCell(new Cell().Add(new Paragraph(factura.FacturaId.ToString())).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph(factura.Anulada ? "Anulada" : "Activa")).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph(factura.Fecha.ToString("dd/MM/yyyy HH:mm"))).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph(ObtenerTipoDTETexto(factura.TipoDTE))).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph(factura.CodigoGeneracion)).SetFontSize(8));
                    table.AddCell(new Cell().Add(new Paragraph(factura.NumeroControl)).SetFontSize(8));
                    table.AddCell(new Cell().Add(new Paragraph(string.IsNullOrEmpty(factura.SelloRecepcion) ? "Pendiente" : "Recibido")).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph(string.IsNullOrEmpty(factura.SelloAnulacion) ? "—" : "Anulado")).SetTextAlignment(TextAlignment.CENTER));
                    table.AddCell(new Cell().Add(new Paragraph($"${factura.TotalGravado:N2}")).SetTextAlignment(TextAlignment.RIGHT));
                    table.AddCell(new Cell().Add(new Paragraph($"${factura.TotalExento:N2}")).SetTextAlignment(TextAlignment.RIGHT));
                    table.AddCell(new Cell().Add(new Paragraph($"${factura.TotalIva:N2}")).SetTextAlignment(TextAlignment.RIGHT));
                    table.AddCell(new Cell().Add(new Paragraph($"${factura.TotalPagar:N2}")).SetTextAlignment(TextAlignment.RIGHT));
                }

                document.Add(table);

                // Agregar totales
                var totalGravado = facturas.Sum(f => f.TotalGravado);
                var totalExento = facturas.Sum(f => f.TotalExento);
                var totalIva = facturas.Sum(f => f.TotalIva);
                var totalGeneral = facturas.Sum(f => f.TotalPagar);

                document.Add(new Paragraph("").SetMarginTop(20));
                
                var totalesTable = new Table(2).UseAllAvailableWidth();
                totalesTable.AddCell(new Cell().Add(new Paragraph("Total Gravado:")));
                totalesTable.AddCell(new Cell().Add(new Paragraph($"${totalGravado:N2}")).SetTextAlignment(TextAlignment.RIGHT));
                totalesTable.AddCell(new Cell().Add(new Paragraph("Total Exento:")));
                totalesTable.AddCell(new Cell().Add(new Paragraph($"${totalExento:N2}")).SetTextAlignment(TextAlignment.RIGHT));
                totalesTable.AddCell(new Cell().Add(new Paragraph("Total IVA:")));
                totalesTable.AddCell(new Cell().Add(new Paragraph($"${totalIva:N2}")).SetTextAlignment(TextAlignment.RIGHT));
                totalesTable.AddCell(new Cell().Add(new Paragraph("TOTAL GENERAL:")));
                totalesTable.AddCell(new Cell().Add(new Paragraph($"${totalGeneral:N2}")).SetTextAlignment(TextAlignment.RIGHT));
                
                document.Add(totalesTable);

                // Pie de página
                document.Add(new Paragraph("").SetMarginTop(20));
                document.Add(new Paragraph($"Reporte generado el: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}")
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.CENTER));

                document.Close();

                var fileName = $"ReporteFacturas_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";
                TempData["Success"] = "Reporte PDF generado exitosamente";
                return File(memoryStream.ToArray(), "application/pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte PDF: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Método para generar reporte Excel de todas las facturas
        public async Task<IActionResult> OnGetGenerarReporteExcelAsync()
        {
            ExcelPackage.License.SetNonCommercialOrganization("My Noncommercial organization"); //This will also set the Company property to the organization name provided in the argument.
            try
            {
                var facturas = await ObtenerFacturasFiltradasAsync();
                
                if (!facturas.Any())
                {
                    TempData["Error"] = "No hay datos para generar el reporte Excel";
                    return RedirectToPage();
                }

                using var package = new ExcelPackage();


                // If you use EPPlus in a noncommercial context
                // according to the Polyform Noncommercial license:
                


                var worksheet = package.Workbook.Worksheets.Add("Reporte de Facturas");

                // Configurar encabezados
                var headers = new[] { 
                    "ID", "Estado", "Fecha", "Tipo DTE", "Código Generación", 
                    "Número Control", "Sello Recepción", "Sello Anulación",
                    "Total Gravado", "Total Exento", "IVA", "Total a Pagar"
                };

                // Agregar encabezados
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[1, i + 1].Value = headers[i];
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    worksheet.Cells[1, i + 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }

                // Agregar datos
                for (int row = 0; row < facturas.Count; row++)
                {
                    var factura = facturas[row];
                    var excelRow = row + 2;

                    worksheet.Cells[excelRow, 1].Value = factura.FacturaId;
                    worksheet.Cells[excelRow, 2].Value = factura.Anulada ? "Anulada" : "Activa";
                    worksheet.Cells[excelRow, 3].Value = factura.Fecha;
                    worksheet.Cells[excelRow, 4].Value = ObtenerTipoDTETexto(factura.TipoDTE);
                    worksheet.Cells[excelRow, 5].Value = factura.CodigoGeneracion;
                    worksheet.Cells[excelRow, 6].Value = factura.NumeroControl;
                    worksheet.Cells[excelRow, 7].Value = string.IsNullOrEmpty(factura.SelloRecepcion) ? "Pendiente" : "Recibido";
                    worksheet.Cells[excelRow, 8].Value = string.IsNullOrEmpty(factura.SelloAnulacion) ? "" : "Anulado";
                    worksheet.Cells[excelRow, 9].Value = factura.TotalGravado;
                    worksheet.Cells[excelRow, 10].Value = factura.TotalExento;
                    worksheet.Cells[excelRow, 11].Value = factura.TotalIva;
                    worksheet.Cells[excelRow, 12].Value = factura.TotalPagar;

                    // Aplicar formato de moneda a las columnas de montos
                    worksheet.Cells[excelRow, 9].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[excelRow, 10].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[excelRow, 11].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[excelRow, 12].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[excelRow, 12].Style.Font.Bold = true;

                    // Aplicar bordes
                    for (int col = 1; col <= 12; col++)
                    {
                        worksheet.Cells[excelRow, col].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }
                }

                // Agregar totales
                var lastRow = facturas.Count + 2;
                var totalGravado = facturas.Sum(f => f.TotalGravado);
                var totalExento = facturas.Sum(f => f.TotalExento);
                var totalIva = facturas.Sum(f => f.TotalIva);
                var totalGeneral = facturas.Sum(f => f.TotalPagar);

                worksheet.Cells[lastRow + 1, 8].Value = "Total Gravado:";
                worksheet.Cells[lastRow + 1, 8].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 1, 9].Value = totalGravado;
                worksheet.Cells[lastRow + 1, 9].Style.Numberformat.Format = "#,##0.00";

                worksheet.Cells[lastRow + 2, 8].Value = "Total Exento:";
                worksheet.Cells[lastRow + 2, 8].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 2, 9].Value = totalExento;
                worksheet.Cells[lastRow + 2, 9].Style.Numberformat.Format = "#,##0.00";

                worksheet.Cells[lastRow + 3, 8].Value = "Total IVA:";
                worksheet.Cells[lastRow + 3, 8].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 3, 9].Value = totalIva;
                worksheet.Cells[lastRow + 3, 9].Style.Numberformat.Format = "#,##0.00";

                worksheet.Cells[lastRow + 4, 8].Value = "TOTAL GENERAL:";
                worksheet.Cells[lastRow + 4, 8].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 4, 9].Value = totalGeneral;
                worksheet.Cells[lastRow + 4, 9].Style.Font.Bold = true;
                worksheet.Cells[lastRow + 4, 9].Style.Numberformat.Format = "#,##0.00";

                // Autoajustar columnas
                worksheet.Cells.AutoFitColumns();

                // Agregar información del filtro
                worksheet.Cells[lastRow + 6, 1].Value = $"Período: {FechaInicio?.ToString("dd/MM/yyyy")} - {FechaFin?.ToString("dd/MM/yyyy")}";
                worksheet.Cells[lastRow + 7, 1].Value = $"Reporte generado el: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}";

                var fileName = $"ReporteFacturas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var content = package.GetAsByteArray();
                
                Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";
                TempData["Success"] = "Reporte Excel generado exitosamente";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al generar reporte Excel: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}
