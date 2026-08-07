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

        try
        {
            if (factura.Anulada)
                throw new InvalidOperationException("Esta factura ya fue anulada.");
            if (string.IsNullOrWhiteSpace(factura.JsonDte))
                throw new InvalidOperationException("La factura no tiene el JSON DTE original.");
            if (string.IsNullOrWhiteSpace(factura.SelloRecepcion))
                throw new InvalidOperationException("La factura no tiene sello de recepción.");

            var rootOriginal = Newtonsoft.Json.Linq.JObject.Parse(factura.JsonDte);
            var ambienteFiscal = ConfiguracionFeService.NormalizarAmbiente(
                rootOriginal["identificacion"]?.Value<string>("ambiente") ?? factura.AmbienteFiscal ?? string.Empty);
            var configuracion = await _configuracionFe.ObtenerAsync(ambienteFiscal, cancellationToken);
            var jsonInvalidacion = _jsonFactory.CrearInvalidacion(
                factura.JsonDte,
                factura.SelloRecepcion,
                "Anulación solicitada por el cliente",
                User.Identity?.Name ?? "Administrador",
                NormalizarNit(configuracion.Entidad.NitEmisor),
                configuracion.Entidad);

            var resultado = await _dteTransmision.TransmitirInvalidacionAsync(
                factura.FacturaId,
                jsonInvalidacion,
                cancellationToken);

            if (!resultado.Aceptado || string.IsNullOrWhiteSpace(resultado.SelloRecepcion))
                throw new InvalidOperationException(resultado.Error ?? "Hacienda no aceptó la invalidación.");

            factura.JsonAnulacion = jsonInvalidacion;
            factura.SelloAnulacion = resultado.SelloRecepcion;
            factura.Anulada = true;
            factura.EstadoTransmision = DteEstados.Anulado;
            factura.FechaAnulacion = DateTime.UtcNow;

            var destinatario = factura.CorreoReceptor;
            if (string.IsNullOrWhiteSpace(destinatario))
            {
                try
                {
                    var root = Newtonsoft.Json.Linq.JObject.Parse(factura.JsonDte);
                    destinatario = root["receptor"]?.Value<string>("correo");
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    // El JSON ya fue validado por el servicio de transmisión; no se duplica el error aquí.
                }
            }

            if (!string.IsNullOrWhiteSpace(destinatario))
                await _emailQueue.EncolarInvalidacionAsync(factura, jsonInvalidacion, resultado.SelloRecepcion, destinatario, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            TempData["Success"] = "Invalidación aceptada. El PDF y JSON quedaron en la cola de correo.";
        }
        catch (Exception ex)
        {
            TempData["ErrorAnulacion"] = $"Error al anular la factura: {ex.Message}";
        }

        return RedirectToPage("./Index");
    }

    private static string NormalizarNit(string? nit)
        => (nit ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
}
