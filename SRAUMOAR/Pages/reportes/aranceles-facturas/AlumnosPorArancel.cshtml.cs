using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Pages.reportes.aranceles_facturas
{
    [Authorize(Roles = "Administrador,Administracion,Contabilidad")]
    public class AlumnosPorArancelModel : PageModel
    {
        private readonly Contexto _context;

        public AlumnosPorArancelModel(Contexto context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public int? ArancelId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaInicio { get; set; } = DateTime.Today;

        [BindProperty(SupportsGet = true)]
        public DateTime? FechaFin { get; set; } = DateTime.Today;

        public string NombreArancel { get; set; } = string.Empty;
        public List<PagoArancelAlumnoItem> Registros { get; set; } = new();
        public int TotalAlumnosUnicos { get; set; }
        public int TotalOperaciones { get; set; }
        public decimal TotalPagadoArancel { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!ValidarFiltros())
            {
                return RedirectToPage("./Index", new { ArancelId, FechaInicio, FechaFin });
            }

            await CargarDatosAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetGenerarExcelAsync()
        {
            if (!ValidarFiltros())
            {
                return RedirectToPage("./Index", new { ArancelId, FechaInicio, FechaFin });
            }

            await CargarDatosAsync();
            if (!Registros.Any())
            {
                TempData["Error"] = "No hay datos para exportar en el detalle de alumnos por arancel.";
                return RedirectToPage(new { ArancelId, FechaInicio, FechaFin });
            }

            ExcelPackage.License.SetNonCommercialOrganization("SRAUMOAR");
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Alumnos por arancel");

            worksheet.Cells[1, 1].Value = "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO";
            worksheet.Cells[1, 1, 1, 8].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.Font.Size = 14;

            worksheet.Cells[2, 1].Value = "REPORTE DE ALUMNOS QUE PAGARON ARANCEL";
            worksheet.Cells[2, 1, 2, 8].Merge = true;
            worksheet.Cells[2, 1].Style.Font.Bold = true;

            worksheet.Cells[3, 1].Value = $"Arancel: {NombreArancel}";
            worksheet.Cells[3, 1, 3, 8].Merge = true;
            worksheet.Cells[4, 1].Value = $"Periodo: {FechaInicio:dd/MM/yyyy} - {FechaFin:dd/MM/yyyy}";
            worksheet.Cells[4, 1, 4, 8].Merge = true;

            var headers = new[]
            {
                "Alumno",
                "Carnet",
                "Carrera",
                "Fecha factura",
                "No. control",
                "Codigo generacion",
                "Monto arancel",
                "Tipo DTE"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[6, i + 1].Value = headers[i];
                worksheet.Cells[6, i + 1].Style.Font.Bold = true;
            }

            var row = 7;
            foreach (var item in Registros)
            {
                worksheet.Cells[row, 1].Value = item.AlumnoNombre;
                worksheet.Cells[row, 2].Value = item.Carnet;
                worksheet.Cells[row, 3].Value = item.Carrera;
                worksheet.Cells[row, 4].Value = item.FechaFactura;
                worksheet.Cells[row, 4].Style.Numberformat.Format = "dd/MM/yyyy HH:mm";
                worksheet.Cells[row, 5].Value = item.NumeroControl;
                worksheet.Cells[row, 6].Value = item.CodigoGeneracion;
                worksheet.Cells[row, 7].Value = item.MontoArancel;
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 8].Value = item.TipoDteTexto;
                row++;
            }

            worksheet.Cells[row + 1, 1].Value = "Total alumnos unicos:";
            worksheet.Cells[row + 1, 2].Value = TotalAlumnosUnicos;
            worksheet.Cells[row + 2, 1].Value = "Total operaciones:";
            worksheet.Cells[row + 2, 2].Value = TotalOperaciones;
            worksheet.Cells[row + 3, 1].Value = "Total pagado del arancel:";
            worksheet.Cells[row + 3, 2].Value = TotalPagadoArancel;
            worksheet.Cells[row + 3, 2].Style.Numberformat.Format = "#,##0.00";

            worksheet.Cells.AutoFitColumns();
            var bytes = package.GetAsByteArray();
            var fileName = $"AlumnosPorArancel_{ArancelId}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private bool ValidarFiltros()
        {
            if (!ArancelId.HasValue)
            {
                TempData["Error"] = "Debe seleccionar un arancel para ver el detalle de alumnos.";
                return false;
            }

            if (!FechaInicio.HasValue || !FechaFin.HasValue)
            {
                TempData["Error"] = "Debe indicar fecha de inicio y fecha fin.";
                return false;
            }

            if (FechaInicio.Value.Date > FechaFin.Value.Date)
            {
                TempData["Error"] = "La fecha de inicio no puede ser mayor que la fecha fin.";
                return false;
            }

            return true;
        }

        private async Task CargarDatosAsync()
        {
            var arancel = await _context.Aranceles.AsNoTracking().FirstOrDefaultAsync(a => a.ArancelId == ArancelId!.Value);
            NombreArancel = arancel?.Nombre ?? $"Arancel {ArancelId.Value}";

            var fechaInicio = FechaInicio!.Value.Date;
            var fechaFin = FechaFin!.Value.Date.AddDays(1).AddSeconds(-1);

            var facturasValidas = await _context.Facturas
                .AsNoTracking()
                .Where(f => !f.Anulada
                            && !string.IsNullOrEmpty(f.SelloRecepcion)
                            && f.Fecha >= fechaInicio
                            && f.Fecha <= fechaFin)
                .Select(f => new
                {
                    f.CodigoGeneracion,
                    f.Fecha,
                    f.NumeroControl,
                    f.TipoDTE
                })
                .ToListAsync();

            var facturasPorCodigo = facturasValidas
                .Where(f => !string.IsNullOrWhiteSpace(f.CodigoGeneracion))
                .ToDictionary(f => f.CodigoGeneracion, f => f);

            var codigos = facturasPorCodigo.Keys.ToList();

            var cobros = await _context.CobrosArancel
                .AsNoTracking()
                .Include(c => c.Alumno)
                    .ThenInclude(a => a.Carrera)
                .Include(c => c.DetallesCobroArancel!)
                .Where(c => c.CodigoGeneracion != null && codigos.Contains(c.CodigoGeneracion))
                .ToListAsync();

            var registros = new List<PagoArancelAlumnoItem>();
            foreach (var cobro in cobros)
            {
                if (cobro.CodigoGeneracion == null || !facturasPorCodigo.TryGetValue(cobro.CodigoGeneracion, out var factura))
                {
                    continue;
                }

                var detalles = cobro.DetallesCobroArancel?
                    .Where(d => d.ArancelId == ArancelId.Value)
                    .ToList();

                if (detalles == null || !detalles.Any())
                {
                    continue;
                }

                foreach (var detalle in detalles)
                {
                    registros.Add(new PagoArancelAlumnoItem
                    {
                        AlumnoId = cobro.AlumnoId,
                        AlumnoNombre = $"{cobro.Alumno?.Nombres} {cobro.Alumno?.Apellidos}".Trim(),
                        Carnet = cobro.Alumno?.Carnet ?? string.Empty,
                        Carrera = cobro.Alumno?.Carrera?.NombreCarrera ?? string.Empty,
                        FechaFactura = factura.Fecha,
                        NumeroControl = factura.NumeroControl ?? string.Empty,
                        CodigoGeneracion = factura.CodigoGeneracion ?? string.Empty,
                        MontoArancel = detalle.costo,
                        TipoDte = factura.TipoDTE,
                        TipoDteTexto = ObtenerTipoDteTexto(factura.TipoDTE)
                    });
                }
            }

            Registros = registros
                .OrderByDescending(r => r.FechaFactura)
                .ThenBy(r => r.AlumnoNombre)
                .ToList();

            TotalOperaciones = Registros.Count;
            TotalAlumnosUnicos = Registros
                .Where(r => r.AlumnoId.HasValue)
                .Select(r => r.AlumnoId!.Value)
                .Distinct()
                .Count();
            TotalPagadoArancel = Registros.Sum(r => r.MontoArancel);
        }

        private static string ObtenerTipoDteTexto(int tipoDte)
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

        public class PagoArancelAlumnoItem
        {
            public int? AlumnoId { get; set; }
            public string AlumnoNombre { get; set; } = string.Empty;
            public string Carnet { get; set; } = string.Empty;
            public string Carrera { get; set; } = string.Empty;
            public DateTime FechaFactura { get; set; }
            public string NumeroControl { get; set; } = string.Empty;
            public string CodigoGeneracion { get; set; } = string.Empty;
            public decimal MontoArancel { get; set; }
            public int TipoDte { get; set; }
            public string TipoDteTexto { get; set; } = string.Empty;
        }
    }
}
