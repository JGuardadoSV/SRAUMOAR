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

    public EditarJsonModel(Contexto context) => _context = context;

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
        if (factura.TipoDTE == 18 || factura.TipoDteCodigo == "18")
        {
            TempData["Error"] = "Los ERET se reanudan desde su flujo controlado y no permiten edición manual del JSON.";
            return RedirectToPage("./Index");
        }
        if (!string.IsNullOrEmpty(factura.SelloRecepcion))
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
        if (factura.TipoDTE == 18 || factura.TipoDteCodigo == "18")
        {
            TempData["Error"] = "No se permite editar manualmente el JSON de un ERET.";
            return RedirectToPage("./Index");
        }
        if (!string.IsNullOrEmpty(factura.SelloRecepcion))
        {
            TempData["Error"] = "Esta factura ya tiene sello de recepción y no puede editarse.";
            return RedirectToPage("./Index");
        }

        if (factura.Anulada || factura.EstadoTransmision != DteEstados.Preparado || factura.IntentosTransmision != 0
            || factura.TipoDteCodigo is "05" or "06" || factura.TipoDTE is 5 or 6
            || await _context.DteTransmisiones.AnyAsync(x => x.FacturaId == id || x.CodigoGeneracion == factura.CodigoGeneracion, cancellationToken)
            || await _context.DteEventos.AnyAsync(x => x.FacturaId == id, cancellationToken))
        {
            TempData["Error"] = "No puede alterarse un JSON con transmisión/reserva fiscal previa. Reintente el documento intacto o utilice una corrección fiscal controlada.";
            return RedirectToPage("./Index");
        }
        var original = JObject.Parse(factura.JsonDte ?? "{}");
        var editado = JObject.Parse(JsonEditado);
        foreach (var campo in new[] { "codigoGeneracion", "numeroControl", "ambiente", "tipoDte", "version" })
            if (!JToken.DeepEquals(original["identificacion"]?[campo], editado["identificacion"]?[campo]))
            {
                TempData["Error"] = "La edición no puede cambiar la identidad, versión ni ambiente fiscal del documento.";
                return RedirectToPage("./Index");
            }
        try
        {
            new DteJsonFactory().ValidarDocumento(JsonEditado, editado["identificacion"]!.Value<string>("ambiente")!);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage("./Index");
        }
        factura.JsonDte = JsonEditado;
        await _context.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        TempData["Success"] = "JSON actualizado. Revise el DTE antes de procesar la contingencia.";
        return RedirectToPage("./EnviarContingencia", new { id = factura.FacturaId });
    }

    public async Task<IActionResult> OnPostConsultarAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var factura = await _context.Facturas.FirstOrDefaultAsync(x => x.FacturaId == id, cancellationToken);
            if (factura is null) return NotFound();

            var transmision = await _context.DteTransmisiones
                .AsNoTracking()
                .Where(x => x.FacturaId == id)
                .OrderByDescending(x => x.FechaInicio)
                .FirstOrDefaultAsync(cancellationToken);

            ConsultaEstado = transmision?.Estado ?? factura.EstadoTransmision;
            ConsultaSello = transmision?.SelloRecepcion ?? factura.SelloRecepcion ?? string.Empty;
            ConsultaMensaje = transmision?.Error ?? "Estado consultado desde el historial local.";
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
