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
                if (tipo=="02")
                {
                    tipo = "03";
                }
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

                // Configurar el documento con márgenes profesionales
                document.SetMargins(30, 30, 30, 30);

                // Encabezado corporativo
                var titulo = new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                    .SetFontSize(18)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(ColorConstants.DARK_GRAY)
                    .SetMarginBottom(10);
                document.Add(titulo);
                 titulo = new Paragraph("REPORTE DE FACTURAS")
                   .SetFontSize(18)
                   .SetTextAlignment(TextAlignment.CENTER)
                   .SetFontColor(ColorConstants.DARK_GRAY)
                   .SetMarginBottom(10);
                document.Add(titulo);

                // Línea decorativa
                var linea = new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                    .SetFontSize(8)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(ColorConstants.GRAY)
                    .SetMarginBottom(15);
                document.Add(linea);

                // Información del filtro con estilo profesional
                var filtroInfo = new Paragraph($"Período de Consulta: {FechaInicio?.ToString("dd/MM/yyyy")} - {FechaFin?.ToString("dd/MM/yyyy")}")
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(ColorConstants.DARK_GRAY)
                    .SetMarginBottom(20);
                document.Add(filtroInfo);

                // Crear tabla con bordes profesionales
               // var table = new Table(12).UseAllAvailableWidth();

                // Definir anchos relativos para cada columna (total debe sumar el número deseado)
                // Crear tabla con anchos específicos directamente en el constructor
                var columnWidths = new float[] {
    0.5f,  // ID
    1f,    // Estado
    1.2f,  // Fecha
    1f,    // Tipo DTE
    1f,    // Código Gen.
    1.2f,  // Número Control
    0.5f,    // Sello Recep.
    1f,    // Sello Anul.
    1.5f,  // Total Gravado
    1.5f,  // Total Exento
    1f,    // IVA
    1.5f   // Total a Pagar
};
                var table = new Table(new UnitValue[] {
    UnitValue.CreatePointValue(25),   // ID - 25pt
    UnitValue.CreatePointValue(50),   // Estado - 50pt
    UnitValue.CreatePointValue(60),   // Fecha - 60pt
    UnitValue.CreatePointValue(50),   // Tipo DTE - 50pt
    UnitValue.CreatePointValue(50),   // Código Gen. - 50pt
    UnitValue.CreatePointValue(70),   // Número Control - 70pt
    UnitValue.CreatePointValue(30),   // Sello Recep. - 30pt (muy pequeño)
    UnitValue.CreatePointValue(50),   // Sello Anul. - 50pt
    UnitValue.CreatePointValue(70),   // Total Gravado - 70pt
    UnitValue.CreatePointValue(70),   // Total Exento - 70pt
    UnitValue.CreatePointValue(40),   // IVA - 40pt
    UnitValue.CreatePointValue(70)    // Total a Pagar - 70pt
}).UseAllAvailableWidth();

                // Configurar encabezados con estilo profesional
                var headers = new[] {
    "ID", "Estado", "Fecha", "Tipo DTE", "Código Gen.",
    "Número Control", "Sello Recep.", "Sello Anul.",
    "Total Gravado", "Total Exento", "IVA", "Total a Pagar"
};

                // Agregar encabezados con estilo mejorado
                foreach (var header in headers)
                {
                    var cell = new Cell()
                        .Add(new Paragraph(header)
                            .SetFontSize(8)
                            .SetFontColor(ColorConstants.WHITE))
                        .SetBackgroundColor(new DeviceRgb(70, 70, 70))
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetPadding(5)
                        .SetBorder(new SolidBorder(ColorConstants.WHITE, 0.5f));
                    table.AddHeaderCell(cell);
                }

                // Agregar datos con estilo alternado
                var isEvenRow = false;
                var i = 0;
                foreach (var factura in facturas)
                {
                        i++;
                    var backgroundColor = isEvenRow ?
                        new DeviceRgb(248, 248, 248) :
                        ColorConstants.WHITE;

                    // Configurar estilo de celda base
                    var cellStyle = new Cell()
                        .SetBackgroundColor(backgroundColor)
                        .SetBorder(new SolidBorder(ColorConstants.LIGHT_GRAY, 0.5f))
                        .SetPadding(3);

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph(i.ToString())
                            .SetFontSize(8)
                            .SetTextAlignment(TextAlignment.CENTER)));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph(factura.Anulada ? "Anulada" : "Activa")
                            .SetFontSize(8)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFontColor(ColorConstants.BLACK)));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph(factura.Fecha.ToString("dd/MM/yyyy HH:mm"))
                            .SetFontSize(8)
                            .SetTextAlignment(TextAlignment.CENTER)));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph(ObtenerTipoDTETexto(factura.TipoDTE))
                            .SetFontSize(8)
                            .SetTextAlignment(TextAlignment.CENTER)));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph(factura.CodigoGeneracion)
                            .SetFontSize(7)
                            .SetTextAlignment(TextAlignment.CENTER)));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph(factura.NumeroControl)
                            .SetFontSize(7)
                            .SetTextAlignment(TextAlignment.CENTER)));

                    string selloTexto = string.IsNullOrEmpty(factura.SelloRecepcion) ? "-" : factura.SelloRecepcion;
                    if (!string.IsNullOrEmpty(factura.SelloRecepcion) && factura.SelloRecepcion.Length > 20)
                    {
                        // Dividir cada 20 caracteres
                        var lineas = new List<string>();
                        for (int ii = 0; ii < factura.SelloRecepcion.Length; ii += 10)
                        {
                            lineas.Add(factura.SelloRecepcion.Substring(ii, Math.Min(10, factura.SelloRecepcion.Length - ii)));
                        }
                        selloTexto = string.Join("\n", lineas);
                    }

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph(selloTexto)
                            .SetFontSize(6)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFontColor(ColorConstants.BLACK)
                            .SetFixedLeading(7)) // Espaciado entre líneas
                        .SetPadding(2));

                    selloTexto = string.IsNullOrEmpty(factura.SelloAnulacion) ? "-" : factura.SelloAnulacion;
                    if (!string.IsNullOrEmpty(factura.SelloAnulacion) && factura.SelloAnulacion.Length > 20)
                    {
                        // Dividir cada 20 caracteres
                        var lineas = new List<string>();
                        for (int ii = 0; ii < factura.SelloAnulacion.Length; ii += 10)
                        {
                            lineas.Add(factura.SelloAnulacion.Substring(ii, Math.Min(10, factura.SelloAnulacion.Length - ii)));
                        }
                        selloTexto = string.Join("\n", lineas);
                    }


                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph(selloTexto)
                            .SetFontSize(6)
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetFontColor(ColorConstants.BLACK)
                            .SetFixedLeading(7)) // Espaciado entre líneas
                        .SetPadding(2));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph($"${factura.TotalGravado:N2}")
                            .SetFontSize(8)
                            .SetTextAlignment(TextAlignment.RIGHT)));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph($"${factura.TotalExento:N2}")
                            .SetFontSize(8)
                            .SetTextAlignment(TextAlignment.RIGHT)));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph($"${factura.TotalIva:N2}")
                            .SetFontSize(8)
                            .SetTextAlignment(TextAlignment.RIGHT)));

                    table.AddCell(cellStyle.Clone(true)
                        .Add(new Paragraph($"${factura.TotalPagar:N2}")
                            .SetFontSize(8)
                            .SetTextAlignment(TextAlignment.RIGHT)
                            .SetFontColor(ColorConstants.BLACK)));

                    isEvenRow = !isEvenRow;
                }

                document.Add(table);

                // Sección de totales con estilo profesional
                var totalGravado = facturas.Sum(f => f.TotalGravado);
                var totalExento = facturas.Sum(f => f.TotalExento);
                var totalIva = facturas.Sum(f => f.TotalIva);
                var totalGeneral = facturas.Sum(f => f.TotalPagar);

                // Espaciado antes de totales
                document.Add(new Paragraph("").SetMarginTop(15));

                // Título de resumen
                var tituloResumen = new Paragraph("RESUMEN FINANCIERO")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(ColorConstants.DARK_GRAY)
                    .SetMarginBottom(10);
                document.Add(tituloResumen);

                // Tabla de totales con estilo profesional
                var totalesTable = new Table(2).UseAllAvailableWidth();
                totalesTable.SetWidth(300);
                totalesTable.SetHorizontalAlignment(HorizontalAlignment.CENTER);

                // Estilo para celdas de totales
                var totalLabelStyle = new Cell()
                    .SetBackgroundColor(new DeviceRgb(240, 240, 240))
                    .SetBorder(new SolidBorder(ColorConstants.GRAY, 0.5f))
                    .SetPadding(8);

                var totalValueStyle = new Cell()
                    .SetBackgroundColor(ColorConstants.WHITE)
                    .SetBorder(new SolidBorder(ColorConstants.GRAY, 0.5f))
                    .SetPadding(8);

                // Agregar filas de totales
                totalesTable.AddCell(totalLabelStyle.Clone(true)
                    .Add(new Paragraph("Total Gravado:")
                        .SetFontSize(10)
                        .SetTextAlignment(TextAlignment.LEFT)));
                totalesTable.AddCell(totalValueStyle.Clone(true)
                    .Add(new Paragraph($"${totalGravado:N2}")
                        .SetFontSize(10)
                        .SetTextAlignment(TextAlignment.RIGHT)));

                totalesTable.AddCell(totalLabelStyle.Clone(true)
                    .Add(new Paragraph("Total Exento:")
                        .SetFontSize(10)
                        .SetTextAlignment(TextAlignment.LEFT)));
                totalesTable.AddCell(totalValueStyle.Clone(true)
                    .Add(new Paragraph($"${totalExento:N2}")
                        .SetFontSize(10)
                        .SetTextAlignment(TextAlignment.RIGHT)));

                totalesTable.AddCell(totalLabelStyle.Clone(true)
                    .Add(new Paragraph("Total IVA:")
                        .SetFontSize(10)
                        .SetTextAlignment(TextAlignment.LEFT)));
                totalesTable.AddCell(totalValueStyle.Clone(true)
                    .Add(new Paragraph($"${totalIva:N2}")
                        .SetFontSize(10)
                        .SetTextAlignment(TextAlignment.RIGHT)));

                // Fila de total general con estilo destacado
                totalesTable.AddCell(totalLabelStyle.Clone(true)
                    .SetBackgroundColor(new DeviceRgb(70, 70, 70))
                    .Add(new Paragraph("TOTAL GENERAL:")
                        .SetFontSize(11)
                        .SetFontColor(ColorConstants.WHITE)
                        .SetTextAlignment(TextAlignment.LEFT)));
                totalesTable.AddCell(totalValueStyle.Clone(true)
                    .SetBackgroundColor(new DeviceRgb(70, 70, 70))
                    .Add(new Paragraph($"${totalGeneral:N2}")
                        .SetFontSize(11)
                        .SetFontColor(ColorConstants.WHITE)
                        .SetTextAlignment(TextAlignment.RIGHT)));

                document.Add(totalesTable);

                // Pie de página profesional
                document.Add(new Paragraph("").SetMarginTop(30));

                var pieLinea = new Paragraph("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")
                    .SetFontSize(8)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(ColorConstants.GRAY)
                    .SetMarginBottom(5);
                document.Add(pieLinea);

                document.Add(new Paragraph($"Reporte generado el: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}")
                    .SetFontSize(9)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(ColorConstants.GRAY));

                document.Add(new Paragraph($"Total de registros: {facturas.Count()}")
                    .SetFontSize(9)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(ColorConstants.GRAY));

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

                // Agregar datos (ordenados por fecha ascendente)
                var facturasOrdenadas = facturas
                    .OrderBy(f => f.Fecha)
                    .ThenBy(f => f.FacturaId)
                    .ToList();
                for (int row = 0; row < facturasOrdenadas.Count; row++)
                {
                    var factura = facturasOrdenadas[row];
                    var excelRow = row + 2;

                    worksheet.Cells[excelRow, 1].Value = factura.FacturaId;
                    worksheet.Cells[excelRow, 2].Value = factura.Anulada ? "Anulada" : "Activa";
                    worksheet.Cells[excelRow, 3].Value = factura.Fecha.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cells[excelRow, 4].Value = ObtenerTipoDTETexto(factura.TipoDTE);
                    worksheet.Cells[excelRow, 5].Value = factura.CodigoGeneracion;
                    worksheet.Cells[excelRow, 6].Value = factura.NumeroControl;
                    worksheet.Cells[excelRow, 7].Value = string.IsNullOrEmpty(factura.SelloRecepcion) ? "Pendiente" : factura.SelloRecepcion;
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
                var totalGravado = facturas.Where(f => !f.Anulada).Sum(f => f.TotalGravado);
                var totalExento = facturas.Where(f => !f.Anulada).Sum(f => f.TotalExento);
                var totalIva = facturas.Where(f => !f.Anulada).Sum(f => f.TotalIva);
                var totalGeneral = facturas.Where(f => !f.Anulada).Sum(f => f.TotalPagar);

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

                // =============================
                // Hoja: ANEXO CONSUMIDOR FINAL
                // (Consolidado por día)
                // =============================
                var wsAnexoCF = package.Workbook.Worksheets.Add("ANEXO CONSUMIDOR FINAL");
                var headersAnexoCF = new[] {
                    "Fecha", "Cantidad", "Primer Código", "Último Código",
                    "Gravado", "Exento", "IVA", "Total"
                };
                for (int i = 0; i < headersAnexoCF.Length; i++)
                {
                    wsAnexoCF.Cells[1, i + 1].Value = headersAnexoCF[i];
                    wsAnexoCF.Cells[1, i + 1].Style.Font.Bold = true;
                    wsAnexoCF.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    wsAnexoCF.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    wsAnexoCF.Cells[1, i + 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }
                var cfValidas = facturas
                    .Where(f => f.TipoDTE == 1 && !f.Anulada && !string.IsNullOrEmpty(f.SelloRecepcion))
                    .ToList();
                var consolidadoCF = cfValidas
                    .GroupBy(f => f.Fecha.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        Fecha = g.Key,
                        Cantidad = g.Count(),
                        PrimerCodigo = g.OrderBy(x => x.Fecha).First().CodigoGeneracion,
                        UltimoCodigo = g.OrderByDescending(x => x.Fecha).First().CodigoGeneracion,
                        Gravado = g.Sum(x => x.TotalGravado),
                        Exento = g.Sum(x => x.TotalExento),
                        Iva = g.Sum(x => x.TotalIva),
                        Total = g.Sum(x => x.TotalPagar)
                    })
                    .ToList();
                var rowCF = 2;
                foreach (var item in consolidadoCF)
                {
                    wsAnexoCF.Cells[rowCF, 1].Value = item.Fecha.ToString("dd/MM/yyyy");
                    wsAnexoCF.Cells[rowCF, 2].Value = item.Cantidad;
                    wsAnexoCF.Cells[rowCF, 3].Value = item.PrimerCodigo ?? "";
                    wsAnexoCF.Cells[rowCF, 4].Value = item.UltimoCodigo ?? "";
                    wsAnexoCF.Cells[rowCF, 5].Value = item.Gravado;
                    wsAnexoCF.Cells[rowCF, 6].Value = item.Exento;
                    wsAnexoCF.Cells[rowCF, 7].Value = item.Iva;
                    wsAnexoCF.Cells[rowCF, 8].Value = item.Total;

                    wsAnexoCF.Cells[rowCF, 5].Style.Numberformat.Format = "#,##0.00";
                    wsAnexoCF.Cells[rowCF, 6].Style.Numberformat.Format = "#,##0.00";
                    wsAnexoCF.Cells[rowCF, 7].Style.Numberformat.Format = "#,##0.00";
                    wsAnexoCF.Cells[rowCF, 8].Style.Numberformat.Format = "#,##0.00";

                    for (int c = 1; c <= 8; c++)
                    {
                        wsAnexoCF.Cells[rowCF, c].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }
                    rowCF++;
                }
                // Totales consolidado CF
                wsAnexoCF.Cells[rowCF, 1].Value = "";
                rowCF++;
                var totalCantCF = consolidadoCF.Sum(x => x.Cantidad);
                var totalGravadoCF = consolidadoCF.Sum(x => x.Gravado);
                var totalExentoCF = consolidadoCF.Sum(x => x.Exento);
                var totalIvaCF = consolidadoCF.Sum(x => x.Iva);
                var totalGeneralCF = consolidadoCF.Sum(x => x.Total);

                wsAnexoCF.Cells[rowCF, 1].Value = "TOTALES:";
                wsAnexoCF.Cells[rowCF, 2].Value = totalCantCF;
                wsAnexoCF.Cells[rowCF, 5].Value = totalGravadoCF;
                wsAnexoCF.Cells[rowCF, 6].Value = totalExentoCF;
                wsAnexoCF.Cells[rowCF, 7].Value = totalIvaCF;
                wsAnexoCF.Cells[rowCF, 8].Value = totalGeneralCF;

                wsAnexoCF.Cells[rowCF, 5].Style.Numberformat.Format = "#,##0.00";
                wsAnexoCF.Cells[rowCF, 6].Style.Numberformat.Format = "#,##0.00";
                wsAnexoCF.Cells[rowCF, 7].Style.Numberformat.Format = "#,##0.00";
                wsAnexoCF.Cells[rowCF, 8].Style.Numberformat.Format = "#,##0.00";

                var totalRangeCF = wsAnexoCF.Cells[rowCF, 1, rowCF, 8];
                totalRangeCF.Style.Font.Bold = true;
                totalRangeCF.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                totalRangeCF.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                totalRangeCF.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                wsAnexoCF.Cells.AutoFitColumns();

                // =============================
                // Hoja: ANEXO CONTRIBUYENTES
                // (Detalle individual CCF válidas)
                // =============================
                var wsAnexoCCF = package.Workbook.Worksheets.Add("ANEXO CONTRIBUYENTES");
                var headersAnexoCCF = new[] {
                    "Fecha", "Tipo", "Número Control", "Código Generación", "Sello Recepción",
                    "Gravado", "Exento", "IVA", "Total", "Estado"
                };
                for (int i = 0; i < headersAnexoCCF.Length; i++)
                {
                    wsAnexoCCF.Cells[1, i + 1].Value = headersAnexoCCF[i];
                    wsAnexoCCF.Cells[1, i + 1].Style.Font.Bold = true;
                    wsAnexoCCF.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    wsAnexoCCF.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    wsAnexoCCF.Cells[1, i + 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }
                var ccfValidas = facturas
                    .Where(f => f.TipoDTE == 3 && !f.Anulada && !string.IsNullOrEmpty(f.SelloRecepcion))
                    .OrderBy(f => f.Fecha)
                    .ToList();
                var rowCCF = 2;
                foreach (var f in ccfValidas)
                {
                    wsAnexoCCF.Cells[rowCCF, 1].Value = f.Fecha.ToString("dd/MM/yyyy");
                    wsAnexoCCF.Cells[rowCCF, 2].Value = "CCF";
                    wsAnexoCCF.Cells[rowCCF, 3].Value = f.NumeroControl ?? "";
                    wsAnexoCCF.Cells[rowCCF, 4].Value = f.CodigoGeneracion ?? "";
                    wsAnexoCCF.Cells[rowCCF, 5].Value = f.SelloRecepcion ?? "";
                    wsAnexoCCF.Cells[rowCCF, 6].Value = f.TotalGravado;
                    wsAnexoCCF.Cells[rowCCF, 7].Value = f.TotalExento;
                    wsAnexoCCF.Cells[rowCCF, 8].Value = f.TotalIva;
                    wsAnexoCCF.Cells[rowCCF, 9].Value = f.TotalPagar;
                    wsAnexoCCF.Cells[rowCCF, 10].Value = "Válida";

                    wsAnexoCCF.Cells[rowCCF, 6].Style.Numberformat.Format = "#,##0.00";
                    wsAnexoCCF.Cells[rowCCF, 7].Style.Numberformat.Format = "#,##0.00";
                    wsAnexoCCF.Cells[rowCCF, 8].Style.Numberformat.Format = "#,##0.00";
                    wsAnexoCCF.Cells[rowCCF, 9].Style.Numberformat.Format = "#,##0.00";

                    for (int c = 1; c <= 10; c++)
                    {
                        wsAnexoCCF.Cells[rowCCF, c].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }
                    rowCCF++;
                }
                // Totales CCF válidas
                wsAnexoCCF.Cells[rowCCF, 1].Value = "";
                rowCCF++;
                var totalGravadoCCF = ccfValidas.Sum(x => x.TotalGravado);
                var totalExentoCCF = ccfValidas.Sum(x => x.TotalExento);
                var totalIvaCCF = ccfValidas.Sum(x => x.TotalIva);
                var totalGeneralCCF = ccfValidas.Sum(x => x.TotalPagar);
                var totalRegsCCF = ccfValidas.Count;

                wsAnexoCCF.Cells[rowCCF, 1].Value = "TOTALES:";
                wsAnexoCCF.Cells[rowCCF, 6].Value = totalGravadoCCF;
                wsAnexoCCF.Cells[rowCCF, 7].Value = totalExentoCCF;
                wsAnexoCCF.Cells[rowCCF, 8].Value = totalIvaCCF;
                wsAnexoCCF.Cells[rowCCF, 9].Value = totalGeneralCCF;
                wsAnexoCCF.Cells[rowCCF, 10].Value = $"Registros: {totalRegsCCF}";

                wsAnexoCCF.Cells[rowCCF, 6].Style.Numberformat.Format = "#,##0.00";
                wsAnexoCCF.Cells[rowCCF, 7].Style.Numberformat.Format = "#,##0.00";
                wsAnexoCCF.Cells[rowCCF, 8].Style.Numberformat.Format = "#,##0.00";
                wsAnexoCCF.Cells[rowCCF, 9].Style.Numberformat.Format = "#,##0.00";

                var totalRangeCCF = wsAnexoCCF.Cells[rowCCF, 1, rowCCF, 10];
                totalRangeCCF.Style.Font.Bold = true;
                totalRangeCCF.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                totalRangeCCF.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                totalRangeCCF.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                wsAnexoCCF.Cells.AutoFitColumns();

                // =====================================
                // Hoja: EXCLUIDAS CONSUMIDOR FINAL (CF)
                // =====================================
                var wsExcluidasCF = package.Workbook.Worksheets.Add("EXCLUIDAS CONSUMIDOR FINAL");
                var headersExcluidas = new[] {
                    "Fecha", "Tipo", "Número Control", "Código Generación", "Sello Recepción",
                    "Gravado", "Exento", "IVA", "Total", "Estado", "Motivo Exclusión", "Verificar Estado"
                };
                for (int i = 0; i < headersExcluidas.Length; i++)
                {
                    wsExcluidasCF.Cells[1, i + 1].Value = headersExcluidas[i];
                    wsExcluidasCF.Cells[1, i + 1].Style.Font.Bold = true;
                    wsExcluidasCF.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    wsExcluidasCF.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    wsExcluidasCF.Cells[1, i + 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }
                var cfExcluidas = facturas
                    .Where(f => f.TipoDTE == 1 && (f.Anulada || string.IsNullOrEmpty(f.SelloRecepcion)))
                    .OrderBy(f => f.Fecha)
                    .ToList();
                var rowExCF = 2;
                foreach (var f in cfExcluidas)
                {
                    var estado = f.Anulada ? "Anulada" : (string.IsNullOrEmpty(f.SelloRecepcion) ? "Pendiente" : "Válida");
                    var motivo = f.Anulada ? "ANULADA" : (string.IsNullOrEmpty(f.SelloRecepcion) ? "SIN SELLO RECEPCION" : "");

                    wsExcluidasCF.Cells[rowExCF, 1].Value = f.Fecha.ToString("dd/MM/yyyy");
                    wsExcluidasCF.Cells[rowExCF, 2].Value = "CF";
                    wsExcluidasCF.Cells[rowExCF, 3].Value = f.NumeroControl ?? "";
                    wsExcluidasCF.Cells[rowExCF, 4].Value = f.CodigoGeneracion ?? "";
                    wsExcluidasCF.Cells[rowExCF, 5].Value = f.SelloRecepcion ?? "";
                    wsExcluidasCF.Cells[rowExCF, 6].Value = f.TotalGravado;
                    wsExcluidasCF.Cells[rowExCF, 7].Value = f.TotalExento;
                    wsExcluidasCF.Cells[rowExCF, 8].Value = f.TotalIva;
                    wsExcluidasCF.Cells[rowExCF, 9].Value = f.TotalPagar;
                    wsExcluidasCF.Cells[rowExCF, 10].Value = estado;
                    wsExcluidasCF.Cells[rowExCF, 11].Value = motivo;

                    // Hipervínculo para verificación pública si hay código de generación
                    if (!string.IsNullOrEmpty(f.CodigoGeneracion))
                    {
                        var url = $"https://admin.factura.gob.sv/consultaPublica?ambiente=01&codGen={f.CodigoGeneracion}&fechaEmi={f.Fecha:yyyy-MM-dd}";
                        wsExcluidasCF.Cells[rowExCF, 12].Value = "VERIFICAR";
                        wsExcluidasCF.Cells[rowExCF, 12].Hyperlink = new Uri(url);
                    }
                    else
                    {
                        wsExcluidasCF.Cells[rowExCF, 12].Value = "N/A";
                    }

                    wsExcluidasCF.Cells[rowExCF, 6].Style.Numberformat.Format = "#,##0.00";
                    wsExcluidasCF.Cells[rowExCF, 7].Style.Numberformat.Format = "#,##0.00";
                    wsExcluidasCF.Cells[rowExCF, 8].Style.Numberformat.Format = "#,##0.00";
                    wsExcluidasCF.Cells[rowExCF, 9].Style.Numberformat.Format = "#,##0.00";

                    for (int c = 1; c <= 12; c++)
                    {
                        wsExcluidasCF.Cells[rowExCF, c].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }

                    rowExCF++;
                }
                // Totales Excluidas CF
                wsExcluidasCF.Cells[rowExCF, 1].Value = "";
                rowExCF++;
                var totalSinSelloCF = cfExcluidas.Where(v => !v.Anulada && string.IsNullOrEmpty(v.SelloRecepcion)).Count();
                var montoSinSelloCF = cfExcluidas.Where(v => !v.Anulada && string.IsNullOrEmpty(v.SelloRecepcion)).Sum(v => v.TotalPagar);
                var totalAnuladasCF = cfExcluidas.Where(v => v.Anulada).Count();
                var montoAnuladasCF = cfExcluidas.Where(v => v.Anulada).Sum(v => v.TotalPagar);
                var totalGeneralExCF = cfExcluidas.Sum(v => v.TotalPagar);

                wsExcluidasCF.Cells[rowExCF, 1].Value = "TOTALES:";
                wsExcluidasCF.Cells[rowExCF, 9].Value = totalGeneralExCF;
                wsExcluidasCF.Cells[rowExCF, 9].Style.Numberformat.Format = "#,##0.00";
                wsExcluidasCF.Cells[rowExCF, 11].Value = $"Sin Sello: {totalSinSelloCF} (${montoSinSelloCF:N2}) | Anuladas: {totalAnuladasCF} (${montoAnuladasCF:N2})";

                var totalRangeExCF = wsExcluidasCF.Cells[rowExCF, 1, rowExCF, 12];
                totalRangeExCF.Style.Font.Bold = true;
                totalRangeExCF.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                totalRangeExCF.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                totalRangeExCF.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                wsExcluidasCF.Cells.AutoFitColumns();

                // ==========================================
                // Hoja: EXCLUIDAS CONTRIBUYENTES (CCF)
                // ==========================================
                var wsExcluidasCCF = package.Workbook.Worksheets.Add("EXCLUIDAS CONTRIBUYENTES");
                for (int i = 0; i < headersExcluidas.Length; i++)
                {
                    wsExcluidasCCF.Cells[1, i + 1].Value = headersExcluidas[i];
                    wsExcluidasCCF.Cells[1, i + 1].Style.Font.Bold = true;
                    wsExcluidasCCF.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    wsExcluidasCCF.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    wsExcluidasCCF.Cells[1, i + 1].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                }
                var ccfExcluidas = facturas
                    .Where(f => f.TipoDTE == 3 && (f.Anulada || string.IsNullOrEmpty(f.SelloRecepcion)))
                    .OrderBy(f => f.Fecha)
                    .ToList();
                var rowExCCF = 2;
                foreach (var f in ccfExcluidas)
                {
                    var estado = f.Anulada ? "Anulada" : (string.IsNullOrEmpty(f.SelloRecepcion) ? "Pendiente" : "Válida");
                    var motivo = f.Anulada ? "ANULADA" : (string.IsNullOrEmpty(f.SelloRecepcion) ? "SIN SELLO RECEPCION" : "");

                    wsExcluidasCCF.Cells[rowExCCF, 1].Value = f.Fecha.ToString("dd/MM/yyyy");
                    wsExcluidasCCF.Cells[rowExCCF, 2].Value = "CCF";
                    wsExcluidasCCF.Cells[rowExCCF, 3].Value = f.NumeroControl ?? "";
                    wsExcluidasCCF.Cells[rowExCCF, 4].Value = f.CodigoGeneracion ?? "";
                    wsExcluidasCCF.Cells[rowExCCF, 5].Value = f.SelloRecepcion ?? "";
                    wsExcluidasCCF.Cells[rowExCCF, 6].Value = f.TotalGravado;
                    wsExcluidasCCF.Cells[rowExCCF, 7].Value = f.TotalExento;
                    wsExcluidasCCF.Cells[rowExCCF, 8].Value = f.TotalIva;
                    wsExcluidasCCF.Cells[rowExCCF, 9].Value = f.TotalPagar;
                    wsExcluidasCCF.Cells[rowExCCF, 10].Value = estado;
                    wsExcluidasCCF.Cells[rowExCCF, 11].Value = motivo;

                    if (!string.IsNullOrEmpty(f.CodigoGeneracion))
                    {
                        var url = $"https://admin.factura.gob.sv/consultaPublica?ambiente=01&codGen={f.CodigoGeneracion}&fechaEmi={f.Fecha:yyyy-MM-dd}";
                        wsExcluidasCCF.Cells[rowExCCF, 12].Value = "VERIFICAR";
                        wsExcluidasCCF.Cells[rowExCCF, 12].Hyperlink = new Uri(url);
                    }
                    else
                    {
                        wsExcluidasCCF.Cells[rowExCCF, 12].Value = "N/A";
                    }

                    wsExcluidasCCF.Cells[rowExCCF, 6].Style.Numberformat.Format = "#,##0.00";
                    wsExcluidasCCF.Cells[rowExCCF, 7].Style.Numberformat.Format = "#,##0.00";
                    wsExcluidasCCF.Cells[rowExCCF, 8].Style.Numberformat.Format = "#,##0.00";
                    wsExcluidasCCF.Cells[rowExCCF, 9].Style.Numberformat.Format = "#,##0.00";

                    for (int c = 1; c <= 12; c++)
                    {
                        wsExcluidasCCF.Cells[rowExCCF, c].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }

                    rowExCCF++;
                }
                // Totales Excluidas CCF
                wsExcluidasCCF.Cells[rowExCCF, 1].Value = "";
                rowExCCF++;
                var totalSinSelloCCF = ccfExcluidas.Where(v => !v.Anulada && string.IsNullOrEmpty(v.SelloRecepcion)).Count();
                var montoSinSelloCCF = ccfExcluidas.Where(v => !v.Anulada && string.IsNullOrEmpty(v.SelloRecepcion)).Sum(v => v.TotalPagar);
                var totalAnuladasCCF2 = ccfExcluidas.Where(v => v.Anulada).Count();
                var montoAnuladasCCF2 = ccfExcluidas.Where(v => v.Anulada).Sum(v => v.TotalPagar);
                var totalGeneralExCCF = ccfExcluidas.Sum(v => v.TotalPagar);

                wsExcluidasCCF.Cells[rowExCCF, 1].Value = "TOTALES:";
                wsExcluidasCCF.Cells[rowExCCF, 9].Value = totalGeneralExCCF;
                wsExcluidasCCF.Cells[rowExCCF, 9].Style.Numberformat.Format = "#,##0.00";
                wsExcluidasCCF.Cells[rowExCCF, 11].Value = $"Sin Sello: {totalSinSelloCCF} (${montoSinSelloCCF:N2}) | Anuladas: {totalAnuladasCCF2} (${montoAnuladasCCF2:N2})";

                var totalRangeExCCF = wsExcluidasCCF.Cells[rowExCCF, 1, rowExCCF, 12];
                totalRangeExCCF.Style.Font.Bold = true;
                totalRangeExCCF.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                totalRangeExCCF.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                totalRangeExCCF.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                wsExcluidasCCF.Cells.AutoFitColumns();

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
