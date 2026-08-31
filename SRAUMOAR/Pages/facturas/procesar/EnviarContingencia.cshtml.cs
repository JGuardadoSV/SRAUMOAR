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
        Factura = factura;
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

        var tieneReclamacion = false;
        var jsonOriginalLeido = factura.JsonDte;
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

            if (factura.Anulada || factura.EstadoTransmision != DteEstados.Preparado)
                throw new InvalidOperationException("Solo puede reintentarse un documento preparado sin envío incierto. Los enviados, inciertos o rechazados requieren conciliación/corrección controlada.");

            var previa = await _context.DteTransmisiones.AsNoTracking().SingleOrDefaultAsync(x =>
                x.CodigoGeneracion == codigoGeneracion && x.AmbienteFiscal == ambienteFiscal && x.TipoOperacion == "EMISION");
            if (previa is not null && (previa.Estado != DteEstados.Preparado || !JToken.DeepEquals(JObject.Parse(previa.JsonEnviado), root)))
                throw new InvalidOperationException("Existe una transmisión previa. No puede alterarse ni reenviarse su JSON sin resolver primero su estado.");

            // Un fallo previo a Hacienda se reintenta con EXACTAMENTE el JSON reservado.
            // La contingencia solo se prepara si nunca se registró una emisión.
            string? jsonEvento = null;
            if (previa is null)
            {
                var eventoPrevio = await _context.DteEventos.AsNoTracking().Where(x => x.FacturaId == id
                    && x.AmbienteFiscal == ambienteFiscal && x.TipoEvento == "CONTINGENCIA")
                    .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
                jsonEvento = eventoPrevio?.JsonEvento ?? _jsonFactory.CrearContingencia(
                factura.JsonDte,
                4,
                motivo,
                User.Identity?.Name ?? "Administrador",
                configuracion.Entidad.NitEmisor ?? string.Empty,
                configuracion.Entidad);
                if (eventoPrevio is null)
                {
                    identificacion["tipoModelo"] = 2;
                    identificacion["tipoOperacion"] = 2;
                    identificacion["tipoContingencia"] = 4;
                    identificacion["motivoContin"] = motivo;
                }
            }
            var jsonEnviado = root.ToString(Formatting.None);
            _jsonFactory.ValidarDocumento(jsonEnviado, ambienteFiscal, tipoDte);
            var reclamada = await _context.Facturas.Where(x => x.FacturaId == id && !x.Anulada
                && x.EstadoTransmision == DteEstados.Preparado && (x.SelloRecepcion == null || x.SelloRecepcion == "")
                && EF.Functions.Collate(x.JsonDte!, "Latin1_General_100_BIN2") == jsonOriginalLeido)
                .ExecuteUpdateAsync(x => x.SetProperty(f => f.EstadoTransmision, DteEstados.Enviado)
                    .SetProperty(f => f.JsonDte, jsonEnviado));
            if (reclamada != 1) throw new InvalidOperationException("Otro proceso está trabajando con el documento.");
            tieneReclamacion = true;
            await _context.Entry(factura).ReloadAsync();
            if (jsonEvento is not null)
            {
                var contingencia = await _dteTransmision.TransmitirContingenciaAsync(factura.FacturaId, jsonEvento);
                if (!contingencia.Aceptado)
                {
                    factura.EstadoTransmision = contingencia.Estado == DteEstados.Preparado ? DteEstados.Preparado : DteEstados.Incierto;
                    factura.RespuestaHacienda = contingencia.Respuesta;
                    await _context.SaveChangesAsync();
                    TempData["Error"] = $"La contingencia no fue aceptada: {contingencia.Error ?? contingencia.Respuesta}";
                    return Page();
                }
            }
            var resultado = await _dteTransmision.TransmitirAsync(new DteTransmisionRequest(
                jsonEnviado, tipoDte, codigoGeneracion, numeroControl, version, correo, factura.FacturaId));

            factura.SelloRecepcion = resultado.SelloRecepcion;
            factura.JsonDte = jsonEnviado;
            factura.AmbienteFiscal = resultado.AmbienteFiscal;
            factura.TipoDteCodigo = tipoDte;
            factura.EstadoTransmision = resultado.Estado;
            factura.IntentosTransmision = resultado.Intentos;
            factura.RespuestaHacienda = resultado.Respuesta;
            factura.FechaAceptacion = resultado.Aceptado ? DateTime.UtcNow : null;
            factura.CorreoReceptor = correo;
            await _context.SaveChangesAsync();

            if (!resultado.Aceptado)
            {
                TempData["Error"] = $"El resultado {resultado.Estado} quedó guardado: {resultado.Error ?? resultado.Respuesta}";
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(correo) && !string.IsNullOrWhiteSpace(resultado.SelloRecepcion))
            {
                try { await _emailQueue.EncolarDteAsync(factura, tipoDte, jsonEnviado, resultado.SelloRecepcion, correo); }
                catch { TempData["Error"] = "DTE aceptado y guardado; no se pudo encolar el correo. Reintente únicamente el correo."; }
            }

            TempData["Success"] = $"DTE enviado en contingencia exitosamente en ambiente {resultado.AmbienteFiscal}.";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            if (tieneReclamacion)
            {
                try
                {
                    // No degradar una aceptación ni reabrir automáticamente una operación incierta.
                    await _context.Facturas.Where(x => x.FacturaId == id && x.EstadoTransmision == DteEstados.Enviado
                        && (x.SelloRecepcion == null || x.SelloRecepcion == ""))
                        .ExecuteUpdateAsync(x => x.SetProperty(f => f.EstadoTransmision, DteEstados.Incierto)
                            .SetProperty(f => f.RespuestaHacienda, "Error durante reproceso; revisar historial de transmisión: " + ex.Message));
                }
                catch { /* No sustituir el error original si tampoco puede registrarse el resultado. */ }
            }
            TempData["Error"] = $"Error durante el proceso de contingencia: {ex.Message}";
            return Page();
        }
    }
}
