using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Modelos;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.facturas;

[Authorize(Roles = "Administrador,Administracion,Contabilidad")]
public sealed class NotaModel : PageModel
{
    private readonly Contexto _contexto;
    private readonly IDteNotaService _notas;

    public NotaModel(Contexto contexto, IDteNotaService notas)
    {
        _contexto = contexto;
        _notas = notas;
    }

    [BindProperty(SupportsGet = true)]
    public int FacturaId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string TipoNota { get; set; } = DteTipos.NotaCredito;

    [BindProperty]
    public decimal? Monto { get; set; }

    [BindProperty]
    public string? Motivo { get; set; }

    public Factura? Factura { get; private set; }
    public DteNotaResultado? Resultado { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await CargarFacturaAsync(cancellationToken)) return NotFound();
        TipoNota = NormalizarTipoNota(TipoNota);
        Monto ??= Factura!.TotalPagar;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        TipoNota = NormalizarTipoNota(TipoNota);
        if (!await CargarFacturaAsync(cancellationToken)) return NotFound();

        if (!ModelState.IsValid)
            return Page();

        try
        {
            Resultado = await _notas.EmitirAsync(new DteNotaRequest(FacturaId, TipoNota, Monto, Motivo), cancellationToken);
            if (!Resultado.Aceptado)
                Error = Resultado.Error ?? "La nota no fue aceptada por Hacienda.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        return Page();
    }

    private async Task<bool> CargarFacturaAsync(CancellationToken cancellationToken)
    {
        Factura = await _contexto.Facturas.AsNoTracking()
            .SingleOrDefaultAsync(x => x.FacturaId == FacturaId, cancellationToken);
        return Factura is not null;
    }

    private static string NormalizarTipoNota(string? tipo)
        => tipo?.Trim() switch
        {
            DteTipos.NotaDebito => DteTipos.NotaDebito,
            _ => DteTipos.NotaCredito
        };
}
