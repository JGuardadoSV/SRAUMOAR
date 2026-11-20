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
            var correo = (root["receptor"] as JObject)?.Value<string>("correo") ?? factura.CorreoReceptor;
            var motivo = string.IsNullOrWhiteSpace(Observaciones) ? "Falla en el sistema de facturacion" : Observaciones.Trim();
            var ambienteFiscal = ConfiguracionFeService.NormalizarAmbiente(
                identificacion.Value<string>("ambiente") ?? factura.AmbienteFiscal ?? string.Empty);
            var configuracion = await _configuracionFe.ObtenerAsync(ambienteFiscal);

            // El JSON actual es la única fuente de verdad. Se puede editar y
            // volver a procesar mientras la factura no tenga sello de recibido.
            var jsonEvento = _jsonFactory.CrearContingencia(
                factura.JsonDte,
                4,
                motivo,
                User.Identity?.Name ?? "Administrador",
                configuracion.Entidad.NitEmisor ?? string.Empty,
                configuracion.Entidad);

            identificacion["tipoModelo"] = 2;
            identificacion["tipoOperacion"] = 2;
            identificacion["tipoContingencia"] = 4;
            identificacion["motivoContin"] = motivo;
            var jsonEnviado = DteJsonNormalizer.Normalizar(root.ToString(Formatting.None));
            _jsonFactory.ValidarDocumento(jsonEnviado, ambienteFiscal, tipoDte);
            factura.JsonDte = jsonEnviado;
            factura.EstadoTransmision = DteEstados.Preparado;
            factura.RespuestaHacienda = null;
            await _context.SaveChangesAsync();

            var previa = await _context.DteTransmisiones.AsNoTracking().SingleOrDefaultAsync(x =>
                x.CodigoGeneracion == codigoGeneracion && x.AmbienteFiscal == ambienteFiscal && x.TipoOperacion == "EMISION");
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
            // Reabrir siempre la reserva existente. No se bloquea por estado
            // anterior; la única protección es el sello de recibido de la factura.
            if (previa is not null)
            {
                await _context.DteTransmisiones
                    .Where(x => x.Id == previa.Id)
                    .ExecuteUpdateAsync(x => x
                        .SetProperty(t => t.JsonEnviado, jsonEnviado)
                        .SetProperty(t => t.Estado, DteEstados.Preparado)
                        .SetProperty(t => t.Intentos, 0)
                        .SetProperty(t => t.SelloRecepcion, (string?)null)
                        .SetProperty(t => t.Respuesta, (string?)null)
                        .SetProperty(t => t.Error, (string?)null)
                        .SetProperty(t => t.FechaFin, (DateTime?)null));
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
