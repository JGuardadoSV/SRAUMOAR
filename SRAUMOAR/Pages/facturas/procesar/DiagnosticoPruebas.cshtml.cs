using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using OfficeOpenXml;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.facturas.procesar;

[Authorize(Roles = "Administrador,Administracion")]
public sealed class DiagnosticoPruebasModel : PageModel
{
    private readonly Contexto _context;
    private readonly IConfiguracionFeService _configuracion;
    private readonly IDteJsonFactory _jsonFactory;
    private readonly IDteTransmisionService _dteTransmision;
    private readonly IDteEmailQueueService _emailQueue;

    public DiagnosticoPruebasModel(
        Contexto context,
        IConfiguracionFeService configuracion,
        IDteJsonFactory jsonFactory,
        IDteTransmisionService dteTransmision,
        IDteEmailQueueService emailQueue)
    {
        _context = context;
        _configuracion = configuracion;
        _jsonFactory = jsonFactory;
        _dteTransmision = dteTransmision;
        _emailQueue = emailQueue;
    }

    public List<FacturaInfo> FacturasPruebas { get; set; } = new();

    public sealed class FacturaInfo
    {
        public int FacturaId { get; set; }
        public DateTime Fecha { get; set; }
        public string NumeroControl { get; set; } = string.Empty;
        public string CodigoGeneracion { get; set; } = string.Empty;
        public int TipoDTE { get; set; }
        public decimal TotalPagar { get; set; }
        public string ReceptorNombre { get; set; } = string.Empty;
        public string ReceptorCorreo { get; set; } = string.Empty;
        public bool Anulada { get; set; }
        public bool TieneSello { get; set; }
    }

    public async Task OnGetAsync() => await CargarFacturasPruebas();

    public async Task<IActionResult> OnPostAnularAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var configuracion = await _configuracion.ObtenerActivaAsync(cancellationToken);
            if (configuracion.Entidad.AmbienteFiscal != "00")
                throw new InvalidOperationException("La anulación de diagnóstico solo permite el ambiente fiscal 00 activo.");

            var factura = await _context.Facturas.FirstOrDefaultAsync(x => x.FacturaId == id, cancellationToken)
                ?? throw new InvalidOperationException("Factura no encontrada.");
            if (factura.Anulada) throw new InvalidOperationException("La factura ya fue anulada.");
            if (string.IsNullOrWhiteSpace(factura.JsonDte) || string.IsNullOrWhiteSpace(factura.SelloRecepcion))
                throw new InvalidOperationException("La factura necesita JSON DTE y sello de recepción.");

            var jsonInvalidacion = _jsonFactory.CrearInvalidacion(
                factura.JsonDte,
                factura.SelloRecepcion,
                "Anulación de factura emitida en ambiente de pruebas",
                User.Identity?.Name ?? "Administrador",
                configuracion.Entidad.NitEmisor ?? string.Empty,
                configuracion.Entidad);
            var resultado = await _dteTransmision.TransmitirInvalidacionAsync(factura.FacturaId, jsonInvalidacion, cancellationToken);
            if (!resultado.Aceptado || string.IsNullOrWhiteSpace(resultado.SelloRecepcion))
                throw new InvalidOperationException(resultado.Error ?? "La invalidación no fue aceptada.");

            factura.JsonAnulacion = jsonInvalidacion;
            factura.SelloAnulacion = resultado.SelloRecepcion;
            factura.Anulada = true;
            factura.EstadoTransmision = DteEstados.Anulado;
            factura.FechaAnulacion = DateTime.UtcNow;
            var correo = factura.CorreoReceptor ?? JObject.Parse(factura.JsonDte)["receptor"]?.Value<string>("correo");
            if (!string.IsNullOrWhiteSpace(correo))
                await _emailQueue.EncolarInvalidacionAsync(factura, jsonInvalidacion, resultado.SelloRecepcion, correo, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = $"Factura {factura.FacturaId} invalidada en ambiente 00.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al anular la factura: {ex.Message}";
        }

        await CargarFacturasPruebas();
        return Page();
    }

    public async Task<IActionResult> OnPostProcesarAsync(int id)
    {
        TempData["Error"] = "La conversión automática de una factura de 00 a 01 fue retirada. Seleccione el ambiente fiscal activo en Configuración FE y emita un nuevo DTE desde el flujo correspondiente.";
        await CargarFacturasPruebas();
        return Page();
    }

    public async Task<IActionResult> OnGetGenerarExcelAsync()
    {
        ExcelPackage.License.SetNonCommercialOrganization("SRAUMOAR");
        try
        {
            await CargarFacturasPruebas();
            if (FacturasPruebas.Count == 0)
            {
                TempData["Error"] = "No hay facturas de pruebas para generar el reporte Excel.";
                return RedirectToPage();
            }

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Facturas de Pruebas");
            var headers = new[] { "ID", "Fecha", "Estado", "Tipo DTE", "Número Control", "Código Generación", "Receptor", "Correo Receptor", "Total" };
            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            }

            for (var index = 0; index < FacturasPruebas.Count; index++)
            {
                var item = FacturasPruebas[index];
                var row = index + 2;
                worksheet.Cells[row, 1].Value = item.FacturaId;
                worksheet.Cells[row, 2].Value = item.Fecha.ToString("dd/MM/yyyy HH:mm");
                worksheet.Cells[row, 3].Value = item.Anulada ? "Anulada" : item.TieneSello ? "Con sello" : "Sin sello";
                worksheet.Cells[row, 4].Value = item.TipoDTE;
                worksheet.Cells[row, 5].Value = item.NumeroControl;
                worksheet.Cells[row, 6].Value = item.CodigoGeneracion;
                worksheet.Cells[row, 7].Value = item.ReceptorNombre;
                worksheet.Cells[row, 8].Value = item.ReceptorCorreo;
                worksheet.Cells[row, 9].Value = item.TotalPagar;
            }
            worksheet.Cells.AutoFitColumns();
            var nombre = $"FacturasPruebas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al generar reporte Excel: {ex.Message}";
            return RedirectToPage();
        }
    }

    private async Task CargarFacturasPruebas()
    {
        var facturas = await _context.Facturas
            .Where(x => !string.IsNullOrEmpty(x.JsonDte))
            .OrderByDescending(x => x.Fecha)
            .ToListAsync();

        FacturasPruebas = facturas
            .Select(ConstruirInfo)
            .Where(x => x is not null)
            .Cast<FacturaInfo>()
            .ToList();
    }

    private static FacturaInfo? ConstruirInfo(Factura factura)
    {
        try
        {
            var root = JObject.Parse(factura.JsonDte!);
            if (root["identificacion"]?.Value<string>("ambiente") != "00") return null;
            var receptor = root["receptor"];
            return new FacturaInfo
            {
                FacturaId = factura.FacturaId,
                Fecha = factura.Fecha,
                NumeroControl = factura.NumeroControl,
                CodigoGeneracion = factura.CodigoGeneracion,
                TipoDTE = factura.TipoDTE,
                TotalPagar = factura.TotalPagar,
                ReceptorNombre = receptor?.Value<string>("nombre") ?? "N/A",
                ReceptorCorreo = receptor?.Value<string>("correo") ?? "N/A",
                Anulada = factura.Anulada,
                TieneSello = !string.IsNullOrWhiteSpace(factura.SelloRecepcion)
            };
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }
}
