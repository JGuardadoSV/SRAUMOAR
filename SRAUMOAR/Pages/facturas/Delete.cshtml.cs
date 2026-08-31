using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.facturas;

[Authorize(Roles = "Administrador,Administracion,Contabilidad")]
public sealed class DeleteModel : PageModel
{
    private readonly Contexto _context;
    private readonly IConfiguracionFeService _configuracionFe;
    private readonly IDteJsonFactory _jsonFactory;
    private readonly IDteTransmisionService _dteTransmision;
    private readonly IDteEmailQueueService _emailQueue;

    public DeleteModel(
        Contexto context,
        IConfiguracionFeService configuracionFe,
        IDteJsonFactory jsonFactory,
        IDteTransmisionService dteTransmision,
        IDteEmailQueueService emailQueue)
    {
        _context = context;
        _configuracionFe = configuracionFe;
        _jsonFactory = jsonFactory;
        _dteTransmision = dteTransmision;
        _emailQueue = emailQueue;
    }

    [BindProperty]
    public Factura Factura { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return NotFound();
        var factura = await _context.Facturas.AsNoTracking().FirstOrDefaultAsync(x => x.FacturaId == id);
        if (factura is null) return NotFound();
        Factura = factura;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken)
    {
        if (id is null) return NotFound();

        var factura = await _context.Facturas.FirstOrDefaultAsync(x => x.FacturaId == id, cancellationToken);
        if (factura is null) return NotFound();
        var esAjax = string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (factura.Anulada)
                throw new InvalidOperationException("Esta factura ya fue anulada.");
            if (string.IsNullOrWhiteSpace(factura.JsonDte))
                throw new InvalidOperationException("La factura no tiene el JSON DTE original.");
            if (string.IsNullOrWhiteSpace(factura.SelloRecepcion))
                throw new InvalidOperationException("La factura no tiene sello de recepción.");

            var rootOriginal = Newtonsoft.Json.Linq.JObject.Parse(factura.JsonDte);
            var tipoDocumentoFiscal = rootOriginal["identificacion"]?.Value<string>("tipoDte")
                ?? rootOriginal["identificacion"]?.Value<string>("tipoEvento");
            var esEventoRetorno = string.Equals(tipoDocumentoFiscal, DteTipos.EventoRetorno, StringComparison.Ordinal);
            var ambienteFiscal = ConfiguracionFeService.NormalizarAmbiente(
                rootOriginal["identificacion"]?.Value<string>("ambiente") ?? factura.AmbienteFiscal ?? string.Empty);
            var configuracion = await _configuracionFe.ObtenerAsync(ambienteFiscal, cancellationToken);
            var reanudando = factura.EstadoTransmision == DteEstados.Anulando && !string.IsNullOrWhiteSpace(factura.JsonAnulacion);
            if (reanudando)
                throw new InvalidOperationException("La invalidación ya fue enviada o quedó incierta. Debe conciliarse con Hacienda usando el mismo código; no se retransmitirá automáticamente.");
            if (!reanudando && !DteEstadoFiscal.EsAceptado(factura))
                throw new InvalidOperationException("El documento no está aceptado o ya tiene una invalidación en proceso.");
            var jsonInvalidacion = _jsonFactory.CrearInvalidacion(
                factura.JsonDte,
                factura.SelloRecepcion,
                "Anulación solicitada por el cliente",
                User.Identity?.Name ?? "Administrador",
                NormalizarNit(configuracion.Entidad.NitEmisor),
                configuracion.Entidad);

            await using (var transaccion = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            {
                var reclamada = await _context.Facturas
                    .Where(x => x.FacturaId == factura.FacturaId && !x.Anulada && x.SelloRecepcion != null && x.SelloRecepcion != ""
                        && (x.EstadoTransmision == DteEstados.Aceptado
                            || x.EstadoTransmision == DteEstados.Preparado && x.IntentosTransmision == 0))
                    .ExecuteUpdateAsync(x => x
                        .SetProperty(f => f.EstadoTransmision, DteEstados.Anulando)
                        .SetProperty(f => f.JsonAnulacion, jsonInvalidacion), cancellationToken);
                if (reclamada != 1)
                    throw new InvalidOperationException("El documento ya no está aceptado o está siendo anulado por otra solicitud.");

                if (!esEventoRetorno)
                {
                    var tieneRetornosActivos = await _context.Facturas.AsNoTracking().AnyAsync(x =>
                        x.FacturaOriginalId == factura.FacturaId && x.TipoDteCodigo == DteTipos.EventoRetorno && !x.Anulada
                        && x.EstadoTransmision != DteEstados.Rechazado, cancellationToken);
                    if (tieneRetornosActivos)
                        throw new InvalidOperationException("No puede invalidarse el documento mientras tenga eventos de retorno vigentes o en proceso.");
                }
                await transaccion.CommitAsync(cancellationToken);
            }
            _context.ChangeTracker.Clear();
            factura = await _context.Facturas.SingleAsync(x => x.FacturaId == id.Value, cancellationToken);
            // Desde este punto cualquier excepción se considera incierta y conserva el mismo JSON/código.
            // Solo un rechazo fiscal explícito libera el documento para otro intento.

            var resultado = await _dteTransmision.TransmitirInvalidacionAsync(
                factura.FacturaId,
                jsonInvalidacion,
                cancellationToken);

            if (!resultado.Aceptado || string.IsNullOrWhiteSpace(resultado.SelloRecepcion))
            {
                if (resultado.Estado is DteEstados.Rechazado or DteEstados.Preparado)
                {
                    await _context.Facturas.Where(x => x.FacturaId == factura.FacturaId && !x.Anulada && x.EstadoTransmision == DteEstados.Anulando)
                        .ExecuteUpdateAsync(x => x
                            .SetProperty(f => f.EstadoTransmision, DteEstados.Aceptado)
                            .SetProperty(f => f.JsonAnulacion, (string?)null), cancellationToken);
                }
                throw new InvalidOperationException(resultado.Error ?? "Hacienda no aceptó la invalidación.");
            }

            factura.JsonAnulacion = jsonInvalidacion;
            factura.SelloAnulacion = resultado.SelloRecepcion;
            factura.Anulada = true;
            factura.EstadoTransmision = DteEstados.Anulado;
            factura.FechaAnulacion = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            // En ERET el receptor está en "documento", no en "receptor". Se prioriza
            // el correo del JSON aceptado para que la invalidación llegue al titular correcto.
            var destinatario = esEventoRetorno
                ? rootOriginal["documento"]?.Value<string>("correo")
                : factura.CorreoReceptor;
            if (string.IsNullOrWhiteSpace(destinatario))
                destinatario = esEventoRetorno
                    ? rootOriginal["documento"]?.Value<string>("correo")
                    : rootOriginal["receptor"]?.Value<string>("correo");
            if (string.IsNullOrWhiteSpace(destinatario))
                destinatario = factura.CorreoReceptor;

            var correoEncolado = false;
            if (!string.IsNullOrWhiteSpace(destinatario))
            {
                try
                {
                    await _emailQueue.EncolarInvalidacionAsync(factura, jsonInvalidacion, resultado.SelloRecepcion, destinatario, cancellationToken);
                    correoEncolado = true;
                }
                catch
                {
                    // La invalidación ya aceptada y persistida no se revierte por un fallo de correo.
                }
            }

            var mensaje = correoEncolado
                ? "Invalidación aceptada. El PDF y JSON quedaron en la cola de correo."
                : "Invalidación aceptada y guardada. No se encoló correo porque falta destinatario o falló la cola.";
            TempData["Success"] = mensaje;
            if (esAjax) return new JsonResult(new { ok = true, mensaje });
        }
        catch (Exception ex)
        {
            var mensaje = $"Error al anular la factura: {ex.Message}";
            TempData["ErrorAnulacion"] = mensaje;
            if (esAjax) return BadRequest(new { ok = false, mensaje });
        }

        return RedirectToPage("./Index");
    }

    private static string NormalizarNit(string? nit)
        => (nit ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
}
