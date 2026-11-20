using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;
using SRAUMOAR.Modelos.FacturacionElectronica;
using Newtonsoft.Json.Linq;

namespace SRAUMOAR.Pages.facturas.procesar;

[Authorize(Roles = "Administrador,Administracion")]
public sealed class EditarJsonModel : PageModel
{
    private readonly Contexto _context;

    private readonly IDteConciliacionService _conciliacion;

    public EditarJsonModel(Contexto context, IDteConciliacionService conciliacion)
    {
        _context = context;
        _conciliacion = conciliacion;
    }

    public Factura Factura { get; set; } = default!;

    [BindProperty]
    public string JsonEditado { get; set; } = string.Empty;

    public string JsonFormateado { get; set; } = string.Empty;
    public string ConsultaResultado { get; set; } = string.Empty;
    public string ConsultaEstado { get; set; } = string.Empty;
    public string ConsultaMensaje { get; set; } = string.Empty;
    public string ConsultaSello { get; set; } = string.Empty;
    public List<string> ConsultaErrores { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is null) return NotFound();
        var factura = await _context.Facturas.FirstOrDefaultAsync(x => x.FacturaId == id);
        if (factura is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(factura.SelloRecepcion))
        {
            TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede editarse.";
            return RedirectToPage("./Index");
        }

        Factura = factura;
        JsonEditado = factura.JsonDte ?? string.Empty;
        try
        {
            JsonFormateado = string.IsNullOrWhiteSpace(JsonEditado)
                ? string.Empty
                : JsonConvert.SerializeObject(JsonConvert.DeserializeObject(JsonEditado), Formatting.Indented);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            JsonFormateado = JsonEditado;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(JsonEditado))
        {
            TempData["Error"] = "El JSON no puede estar vacío.";
            return await OnGetAsync(id);
        }
        try
        {
            JsonConvert.DeserializeObject(JsonEditado);
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            TempData["Error"] = $"JSON inválido: {ex.Message}";
            return await OnGetAsync(id);
        }

        await using var transaccion = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var factura = await _context.Facturas.FirstOrDefaultAsync(x => x.FacturaId == id, cancellationToken);
        if (factura is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(factura.SelloRecepcion))
        {
            TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede editarse.";
            return RedirectToPage("./Index");
        }

        var tieneSelloEnTransmision = await _context.DteTransmisiones.AnyAsync(x =>
            (x.FacturaId == id || x.CodigoGeneracion == factura.CodigoGeneracion)
            && x.TipoOperacion == "EMISION"
            && !string.IsNullOrWhiteSpace(x.SelloRecepcion), cancellationToken);
        if (tieneSelloEnTransmision)
        {
            TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede editarse.";
            return RedirectToPage("./Index");
        }

        var reservaPendiente = await _context.DteTransmisiones
            .FirstOrDefaultAsync(x => (x.FacturaId == id || x.CodigoGeneracion == factura.CodigoGeneracion)
                && x.TipoOperacion == "EMISION"
                && string.IsNullOrWhiteSpace(x.SelloRecepcion), cancellationToken);
        var editado = JObject.Parse(JsonEditado);
        try
        {
            var ambiente = (editado["identificacion"] as JObject)?.Value<string>("ambiente");
            if (string.IsNullOrWhiteSpace(ambiente))
                throw new InvalidOperationException("El JSON debe contener el ambiente fiscal en identificacion.");
            new DteJsonFactory().ValidarDocumento(JsonEditado, ambiente);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage("./Index");
        }
        var identificacionEditada = (editado["identificacion"] as JObject)!;
        var codigoGeneracionEditado = identificacionEditada.Value<string>("codigoGeneracion");
        var numeroControlEditado = identificacionEditada.Value<string>("numeroControl");
        var tipoDteEditado = identificacionEditada.Value<string>("tipoDte");
        var ambienteEditado = identificacionEditada.Value<string>("ambiente");
        factura.JsonDte = JsonEditado;
        factura.CodigoGeneracion = codigoGeneracionEditado ?? factura.CodigoGeneracion;
        factura.NumeroControl = numeroControlEditado ?? factura.NumeroControl;
        factura.TipoDteCodigo = tipoDteEditado ?? factura.TipoDteCodigo;
        if (int.TryParse(tipoDteEditado, out var tipoDteNumerico))
            factura.TipoDTE = tipoDteNumerico;
        factura.AmbienteFiscal = ambienteEditado ?? factura.AmbienteFiscal;
        factura.EstadoTransmision = DteEstados.Preparado;
        factura.IntentosTransmision = 0;
        factura.RespuestaHacienda = null;
        factura.FechaAceptacion = null;
        factura.CorreoReceptor = (editado["receptor"] as JObject)?.Value<string>("correo")
            ?? (editado["sujetoExcluido"] as JObject)?.Value<string>("correo")
            ?? (editado["donante"] as JObject)?.Value<string>("correo");
        if (reservaPendiente is not null)
        {
            reservaPendiente.JsonEnviado = JsonEditado;
            reservaPendiente.CodigoGeneracion = codigoGeneracionEditado ?? reservaPendiente.CodigoGeneracion;
            reservaPendiente.AmbienteFiscal = ambienteEditado ?? reservaPendiente.AmbienteFiscal;
            reservaPendiente.Estado = DteEstados.Preparado;
            reservaPendiente.Intentos = 0;
            reservaPendiente.SelloRecepcion = null;
            reservaPendiente.Respuesta = null;
            reservaPendiente.Error = null;
            reservaPendiente.FechaFin = null;
        }
        await _context.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        TempData["Success"] = "JSON actualizado. Revise el DTE antes de procesar la contingencia.";
        return RedirectToPage("./EnviarContingencia", new { id = factura.FacturaId });
    }

    public async Task<IActionResult> OnPostConsultarAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var mensaje = await _conciliacion.ConciliarAsync(id, cancellationToken);
            _context.ChangeTracker.Clear();
            var factura = await _context.Facturas.FirstOrDefaultAsync(x => x.FacturaId == id, cancellationToken);
            if (factura is null) return NotFound();
            TempData["Success"] = mensaje;

            var transmision = await _context.DteTransmisiones
                .AsNoTracking()
                .Where(x => x.FacturaId == id)
                .OrderByDescending(x => x.FechaInicio)
                .FirstOrDefaultAsync(cancellationToken);

            ConsultaEstado = transmision?.Estado ?? factura.EstadoTransmision;
            ConsultaSello = transmision?.SelloRecepcion ?? factura.SelloRecepcion ?? string.Empty;
            ConsultaMensaje = mensaje;
            ConsultaResultado = transmision?.Respuesta ?? factura.RespuestaHacienda ?? "Sin respuesta registrada.";
            if (!string.IsNullOrWhiteSpace(transmision?.Error)) ConsultaErrores.Add(transmision.Error);

            return await CargarParaPaginaAsync(factura, cancellationToken);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al consultar el historial: {ex.Message}";
            return await OnGetAsync(id);
        }
    }

    private async Task<IActionResult> CargarParaPaginaAsync(Factura factura, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(factura.SelloRecepcion))
        {
            TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede editarse.";
            return RedirectToPage("./Index");
        }
        Factura = factura;
        JsonEditado = factura.JsonDte ?? string.Empty;
        JsonFormateado = JsonEditado;
        await Task.CompletedTask;
        return Page();
    }
}
