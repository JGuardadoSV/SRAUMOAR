using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.reportes.aranceles_facturas
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class IndexModel : PageModel
    {
        private readonly Contexto _context;

        public IndexModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int? ArancelId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedCicloId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; } = DateTime.Today;

        public List<ReporteItem> Registros { get; set; } = new();
        public decimal TotalFacturasValidas { get; set; }
        public decimal TotalArancelesCobrados { get; set; }
        public decimal SubtotalDonaciones { get; set; }

        public async Task OnGetAsync()
        {
            SelectedCicloId = await ObtenerCicloSeleccionadoAsync();

            if (!ValidarRangoFechas())
            {
                await CargarCiclosAsync();
                await CargarArancelesAsync();
                return;
            }

            await CargarCiclosAsync();
            await CargarArancelesAsync();
            await CargarReporteAsync();
        }

        public async Task<IActionResult> OnGetGenerarExcelAsync()
        {
            if (!ValidarRangoFechas())
            {
                return RedirectToPage(new { ArancelId, SelectedCicloId, FechaInicio, FechaFin });
            }

            SelectedCicloId = await ObtenerCicloSeleccionadoAsync();
            await CargarCiclosAsync();
            await CargarArancelesAsync();
            await CargarReporteAsync();
            if (!Registros.Any())
            {
                TempData["Error"] = "No hay datos para exportar a Excel con los filtros seleccionados.";
                return RedirectToPage(new { ArancelId, SelectedCicloId, FechaInicio, FechaFin });
            }

            ExcelPackage.License.SetNonCommercialOrganization("SRAUMOAR");
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Aranceles y Facturas");

            worksheet.Cells[1, 1].Value = "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO";
            worksheet.Cells[1, 1, 1, 9].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.Font.Size = 14;

            worksheet.Cells[2, 1].Value = "REPORTE DE ARANCELES Y FACTURAS VALIDAS";
            worksheet.Cells[2, 1, 2, 9].Merge = true;
            worksheet.Cells[2, 1].Style.Font.Bold = true;

            worksheet.Cells[3, 1].Value = $"Periodo: {FechaInicio:dd/MM/yyyy} - {FechaFin:dd/MM/yyyy}";
            worksheet.Cells[3, 1, 3, 9].Merge = true;

            var headers = new[]
            {
                "Fecha",
                "Tipo DTE",
                "No. Control",
                "Codigo Generacion",
                "Origen",
                "Arancel",
                "Total Factura",
                "Monto Arancel",
                "Es Donacion"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[5, i + 1].Value = headers[i];
                worksheet.Cells[5, i + 1].Style.Font.Bold = true;
            }

            var row = 6;
            foreach (var item in Registros)
            {
                worksheet.Cells[row, 1].Value = item.Fecha;
                worksheet.Cells[row, 1].Style.Numberformat.Format = "dd/MM/yyyy HH:mm";
                worksheet.Cells[row, 2].Value = item.TipoDTETexto;
                worksheet.Cells[row, 3].Value = item.NumeroControl;
                worksheet.Cells[row, 4].Value = item.CodigoGeneracion;
                worksheet.Cells[row, 5].Value = item.Origen;
                worksheet.Cells[row, 6].Value = item.ArancelNombre;
                worksheet.Cells[row, 7].Value = item.TotalFactura;
                worksheet.Cells[row, 8].Value = item.MontoArancel;
                worksheet.Cells[row, 9].Value = item.EsDonacion ? "Si" : "No";

                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";
                row++;
            }

            worksheet.Cells[row + 1, 1].Value = "Total facturas validas emitidas:";
            worksheet.Cells[row + 1, 2].Value = TotalFacturasValidas;
            worksheet.Cells[row + 1, 2].Style.Numberformat.Format = "#,##0.00";

            worksheet.Cells[row + 2, 1].Value = "Total aranceles cobrados:";
            worksheet.Cells[row + 2, 2].Value = TotalArancelesCobrados;
            worksheet.Cells[row + 2, 2].Style.Numberformat.Format = "#,##0.00";

            worksheet.Cells[row + 3, 1].Value = "Subtotal donaciones:";
            worksheet.Cells[row + 3, 2].Value = SubtotalDonaciones;
            worksheet.Cells[row + 3, 2].Style.Numberformat.Format = "#,##0.00";

            worksheet.Cells.AutoFitColumns();

            var bytes = package.GetAsByteArray();
            var fileName = $"Reporte_Aranceles_Facturas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public async Task<IActionResult> OnGetGenerarPDFAsync()
        {
            if (!ValidarRangoFechas())
            {
                return RedirectToPage(new { ArancelId, SelectedCicloId, FechaInicio, FechaFin });
            }

            SelectedCicloId = await ObtenerCicloSeleccionadoAsync();
            await CargarCiclosAsync();
            await CargarArancelesAsync();
            await CargarReporteAsync();
            if (!Registros.Any())
            {
                TempData["Error"] = "No hay datos para exportar a PDF con los filtros seleccionados.";
                return RedirectToPage(new { ArancelId, SelectedCicloId, FechaInicio, FechaFin });
            }

            using var memoryStream = new MemoryStream();
            var writer = new PdfWriter(memoryStream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            document.Add(new Paragraph("UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO")
                .SetFont(bold).SetFontSize(14).SetTextAlignment(TextAlignment.CENTER));
            document.Add(new Paragraph("REPORTE DE ARANCELES Y FACTURAS VALIDAS")
                .SetFont(bold).SetFontSize(12).SetTextAlignment(TextAlignment.CENTER));
            document.Add(new Paragraph($"Periodo: {FechaInicio:dd/MM/yyyy} - {FechaFin:dd/MM/yyyy}")
                .SetFont(normal).SetFontSize(10).SetTextAlignment(TextAlignment.CENTER));

            var table = new Table(new float[] { 1.4f, 0.8f, 1.8f, 1.4f, 1.1f, 1.3f }).UseAllAvailableWidth();
            var headers = new[] { "Fecha", "Tipo", "No. Control", "Origen", "Arancel", "Total" };
            foreach (var header in headers)
            {
                table.AddHeaderCell(new Cell().Add(new Paragraph(header).SetFont(bold).SetFontSize(9)));
            }

            foreach (var item in Registros.Take(500))
            {
                table.AddCell(new Cell().Add(new Paragraph(item.Fecha.ToString("dd/MM/yyyy HH:mm")).SetFont(normal).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.TipoDTETexto).SetFont(normal).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.NumeroControl ?? "").SetFont(normal).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.Origen).SetFont(normal).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.ArancelNombre ?? "-").SetFont(normal).SetFontSize(8)));
                table.AddCell(new Cell().Add(new Paragraph(item.TotalFactura.ToString("N2")).SetFont(normal).SetFontSize(8)).SetTextAlignment(TextAlignment.RIGHT));
            }

            document.Add(table);
            document.Add(new Paragraph($"Total facturas validas emitidas: ${TotalFacturasValidas:N2}").SetFont(bold).SetFontSize(10));
            document.Add(new Paragraph($"Total aranceles cobrados: ${TotalArancelesCobrados:N2}").SetFont(bold).SetFontSize(10));
            document.Add(new Paragraph($"Subtotal donaciones: ${SubtotalDonaciones:N2}").SetFont(bold).SetFontSize(10));

            if (Registros.Count > 500)
            {
                document.Add(new Paragraph("Nota: se muestran las primeras 500 filas en el detalle del PDF.")
                    .SetFont(normal).SetFontSize(8));
            }

            document.Close();
            var fileName = $"Reporte_Aranceles_Facturas_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(memoryStream.ToArray(), "application/pdf", fileName);
        }

        private async Task CargarArancelesAsync()
        {
            var arancelesQuery = _context.Aranceles
                .AsNoTracking()
                .Where(a => a.Activo);

            if (SelectedCicloId.HasValue)
            {
                arancelesQuery = arancelesQuery.Where(a => a.CicloId == SelectedCicloId.Value);
            }

            var aranceles = await arancelesQuery
                .OrderBy(a => a.Nombre)
                .Select(a => new { a.ArancelId, Nombre = a.Nombre ?? $"Arancel {a.ArancelId}" })
                .ToListAsync();

            ViewData["Aranceles"] = new SelectList(aranceles, "ArancelId", "Nombre", ArancelId);
        }

        private async Task CargarCiclosAsync()
        {
            var ciclos = await _context.Ciclos
                .AsNoTracking()
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .Select(c => new
                {
                    c.Id,
                    Nombre = c.Activo
                        ? $"{c.anio} - Ciclo {c.NCiclo} (Activo)"
                        : $"{c.anio} - Ciclo {c.NCiclo}"
                })
                .ToListAsync();

            ViewData["Ciclos"] = new SelectList(ciclos, "Id", "Nombre", SelectedCicloId);
        }

        private async Task<int?> ObtenerCicloSeleccionadoAsync()
        {
            if (SelectedCicloId.HasValue)
            {
                var existe = await _context.Ciclos
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == SelectedCicloId.Value);

                if (existe)
                {
                    return SelectedCicloId;
                }
            }

            var cicloActivo = await _context.Ciclos
                .AsNoTracking()
                .Where(c => c.Activo)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            if (cicloActivo.HasValue)
            {
                return cicloActivo;
            }

            return await _context.Ciclos
                .AsNoTracking()
                .OrderByDescending(c => c.anio)
                .ThenByDescending(c => c.NCiclo)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();
        }

        private bool ValidarRangoFechas()
        {
            if (!FechaInicio.HasValue || !FechaFin.HasValue)
            {
                TempData["Error"] = "Debe seleccionar fecha de inicio y fecha fin.";
                return false;
            }

            if (FechaInicio.Value.Date > FechaFin.Value.Date)
            {
                TempData["Error"] = "La fecha de inicio no puede ser mayor que la fecha fin.";
                return false;
            }

            return true;
        }

        private async Task CargarReporteAsync()
        {
            var fechaInicio = FechaInicio!.Value.Date;
            var fechaFin = FechaFin!.Value.Date.AddDays(1).AddSeconds(-1);
            if (SelectedCicloId.HasValue)
            {
                var ciclo = await _context.Ciclos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == SelectedCicloId.Value);

                if (ciclo != null)
                {
                    var inicioCiclo = ciclo.FechaInicio.Date;
                    var finCiclo = ciclo.FechaFin.Date.AddDays(1).AddSeconds(-1);
                    fechaInicio = fechaInicio < inicioCiclo ? inicioCiclo : fechaInicio;
                    fechaFin = fechaFin > finCiclo ? finCiclo : fechaFin;
                }
            }

            if (fechaInicio > fechaFin)
            {
                Registros = new List<ReporteItem>();
                TotalFacturasValidas = 0;
                TotalArancelesCobrados = 0;
                SubtotalDonaciones = 0;
                return;
            }

            var facturasValidas = await _context.Facturas
                .AsNoTracking()
                .Where(f => !f.Anulada &&
                            !string.IsNullOrEmpty(f.SelloRecepcion) &&
                            f.Fecha >= fechaInicio &&
                            f.Fecha <= fechaFin)
                .OrderByDescending(f => f.Fecha)
                .ToListAsync();

            var codigosGeneracion = facturasValidas
                .Select(f => f.CodigoGeneracion)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .ToList();

            var cobros = await _context.CobrosArancel
                .AsNoTracking()
                .Include(c => c.DetallesCobroArancel!)
                    .ThenInclude(d => d.Arancel)
                .Where(c =>
                    c.CodigoGeneracion != null &&
                    codigosGeneracion.Contains(c.CodigoGeneracion) &&
                    (!SelectedCicloId.HasValue || c.CicloId == SelectedCicloId.Value))
                .ToListAsync();

            var cobrosPorCodigo = cobros
                .Where(c => !string.IsNullOrEmpty(c.CodigoGeneracion))
                .GroupBy(c => c.CodigoGeneracion!)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CobroArancelId).First());

            var registros = new List<ReporteItem>();
            foreach (var factura in facturasValidas)
            {
                cobrosPorCodigo.TryGetValue(factura.CodigoGeneracion, out var cobro);
                var detalles = cobro?.DetallesCobroArancel?.ToList() ?? new List<Entidades.Colecturia.DetallesCobroArancel>();

                if (detalles.Any())
                {
                    foreach (var detalle in detalles)
                    {
                        if (ArancelId.HasValue && detalle.ArancelId != ArancelId.Value)
                        {
                            continue;
                        }

                        registros.Add(new ReporteItem
                        {
                            Fecha = factura.Fecha,
                            TipoDTE = factura.TipoDTE,
                            TipoDTETexto = ObtenerTipoDTETexto(factura.TipoDTE),
                            NumeroControl = factura.NumeroControl,
                            CodigoGeneracion = factura.CodigoGeneracion,
                            Origen = "Arancel",
                            ArancelNombre = detalle.Arancel?.Nombre ?? $"Arancel {detalle.ArancelId}",
                            TotalFactura = factura.TotalPagar,
                            MontoArancel = detalle.costo,
                            EsDonacion = factura.TipoDTE == 15
                        });
                    }
                }
                else if (!ArancelId.HasValue)
                {
                    registros.Add(new ReporteItem
                    {
                        Fecha = factura.Fecha,
                        TipoDTE = factura.TipoDTE,
                        TipoDTETexto = ObtenerTipoDTETexto(factura.TipoDTE),
                        NumeroControl = factura.NumeroControl,
                        CodigoGeneracion = factura.CodigoGeneracion,
                        Origen = "Punto de venta",
                        ArancelNombre = null,
                        TotalFactura = factura.TotalPagar,
                        MontoArancel = 0,
                        EsDonacion = factura.TipoDTE == 15
                    });
                }
            }

            Registros = registros
                .OrderByDescending(r => r.Fecha)
                .ThenBy(r => r.NumeroControl)
                .ToList();

            TotalFacturasValidas = Registros
                .Select(r => r.CodigoGeneracion)
                .Distinct()
                .Join(facturasValidas, codigo => codigo, factura => factura.CodigoGeneracion, (_, factura) => factura.TotalPagar)
                .Sum();

            TotalArancelesCobrados = Registros.Sum(r => r.MontoArancel);
            SubtotalDonaciones = Registros
                .Where(r => r.EsDonacion)
                .Select(r => r.CodigoGeneracion)
                .Distinct()
                .Join(facturasValidas.Where(f => f.TipoDTE == 15), codigo => codigo, factura => factura.CodigoGeneracion, (_, factura) => factura.TotalPagar)
                .Sum();
        }

        private static string ObtenerTipoDTETexto(int tipoDte)
        {
            return tipoDte switch
            {
                1 => "CF",
                2 => "CCF",
                3 => "CCF",
                14 => "SE",
                15 => "DON",
                _ => tipoDte.ToString()
            };
        }

        public class ReporteItem
        {
            public DateTime Fecha { get; set; }
            public int TipoDTE { get; set; }
            public string TipoDTETexto { get; set; } = "";
            public string NumeroControl { get; set; } = "";
            public string CodigoGeneracion { get; set; } = "";
            public string Origen { get; set; } = "";
            public string? ArancelNombre { get; set; }
            public decimal TotalFactura { get; set; }
            public decimal MontoArancel { get; set; }
            public bool EsDonacion { get; set; }
        }
    }
}
