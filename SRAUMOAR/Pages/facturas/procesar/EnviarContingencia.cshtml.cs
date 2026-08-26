using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.facturas.procesar;

[Authorize(Roles = "Administrador,Administracion,Contabilidad")]
public class EnviarContingenciaModel : PageModel
{
    private readonly Contexto _context;
    private readonly IConfiguracionFeService _configuracionFe;
    private readonly IDteJsonFactory _jsonFactory;
    private readonly IDteTransmisionService _dteTransmision;
    private readonly IDteEmailQueueService _emailQueue;

    public EnviarContingenciaModel(Contexto context, IConfiguracionFeService configuracionFe, IDteJsonFactory jsonFactory, IDteTransmisionService dteTransmision, IDteEmailQueueService emailQueue)
    {
        _context = context;
        _configuracionFe = configuracionFe;
        _jsonFactory = jsonFactory;
        _dteTransmision = dteTransmision;
        _emailQueue = emailQueue;
    }

    [BindProperty] public Factura Factura { get; set; } = default!;
    [BindProperty] public string Observaciones { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return NotFound();
        var factura = await _context.Facturas.AsNoTracking().FirstOrDefaultAsync(x => x.FacturaId == id);
        if (factura is null) return NotFound();
        if (factura.TipoDTE == 18 || factura.TipoDteCodigo == DteTipos.EventoRetorno)
        {
            TempData["Error"] = "Los ERET no se procesan mediante el flujo genérico de contingencia.";
            return RedirectToPage("./Index");
        }
        if (!string.IsNullOrEmpty(factura.SelloRecepcion))
        {
            TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede ser procesada.";
            return RedirectToPage("./Index");
        }
        Factura = factura;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var factura = await _context.Facturas.FirstOrDefaultAsync(x => x.FacturaId == id);
        if (factura is null) return NotFound();
        if (factura.TipoDTE == 18 || factura.TipoDteCodigo == DteTipos.EventoRetorno)
        {
            TempData["Error"] = "Los ERET no se procesan mediante el flujo genérico de contingencia.";
            return RedirectToPage("./Index");
        }
        if (!string.IsNullOrEmpty(factura.SelloRecepcion))
        {
            TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede ser procesada.";
            return RedirectToPage("./Index");
        }
        if (string.IsNullOrWhiteSpace(factura.JsonDte))
        {
            TempData["Error"] = "JSON original vacío.";
            return Page();
        }

        try
        {
            var root = JObject.Parse(factura.JsonDte);
            var identificacion = root["identificacion"] as JObject ?? throw new InvalidOperationException("El DTE no tiene identificación.");
            var tipoDte = identificacion.Value<string>("tipoDte") ?? throw new InvalidOperationException("El DTE no tiene tipoDte.");
            var codigoGeneracion = identificacion.Value<string>("codigoGeneracion") ?? throw new InvalidOperationException("El DTE no tiene código de generación.");
            var numeroControl = identificacion.Value<string>("numeroControl") ?? string.Empty;
            var version = identificacion.Value<int?>("version") ?? 1;
            var correo = root["receptor"]?.Value<string>("correo") ?? factura.CorreoReceptor;
            var motivo = string.IsNullOrWhiteSpace(Observaciones) ? "Falla en el sistema de facturacion" : Observaciones.Trim();
            var ambienteFiscal = ConfiguracionFeService.NormalizarAmbiente(
                identificacion.Value<string>("ambiente") ?? factura.AmbienteFiscal ?? string.Empty);
            var configuracion = await _configuracionFe.ObtenerAsync(ambienteFiscal);

            var jsonEvento = _jsonFactory.CrearContingencia(
                factura.JsonDte,
                4,
                motivo,
                User.Identity?.Name ?? "Administrador",
                configuracion.Entidad.NitEmisor ?? string.Empty,
                configuracion.Entidad);
            var contingencia = await _dteTransmision.TransmitirContingenciaAsync(factura.FacturaId, jsonEvento);
            if (!contingencia.Aceptado)
            {
                TempData["Error"] = $"La contingencia no fue aceptada: {contingencia.Error ?? contingencia.Respuesta}";
                return Page();
            }

            identificacion["tipoModelo"] = 2;
            identificacion["tipoOperacion"] = 2;
            identificacion["tipoContingencia"] = 4;
            identificacion["motivoContin"] = motivo;
            var jsonEnviado = root.ToString(Formatting.None);
            var resultado = await _dteTransmision.TransmitirAsync(new DteTransmisionRequest(
                jsonEnviado, tipoDte, codigoGeneracion, numeroControl, version, correo, factura.FacturaId));

            if (!resultado.Aceptado)
            {
                TempData["Error"] = $"El DTE contingente no fue aceptado: {resultado.Error ?? resultado.Respuesta}";
                return Page();
            }

            factura.SelloRecepcion = resultado.SelloRecepcion;
            factura.JsonDte = jsonEnviado;
            factura.AmbienteFiscal = resultado.AmbienteFiscal;
            factura.TipoDteCodigo = tipoDte;
            factura.EstadoTransmision = resultado.Estado;
            factura.IntentosTransmision = resultado.Intentos;
            factura.RespuestaHacienda = resultado.Respuesta;
            factura.FechaAceptacion = DateTime.UtcNow;
            factura.CorreoReceptor = correo;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(correo) && !string.IsNullOrWhiteSpace(resultado.SelloRecepcion))
                await _emailQueue.EncolarDteAsync(factura, tipoDte, jsonEnviado, resultado.SelloRecepcion, correo);

            TempData["Success"] = $"DTE enviado en contingencia exitosamente en ambiente {resultado.AmbienteFiscal}.";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error durante el proceso de contingencia: {ex.Message}";
            return Page();
        }
    }
}
