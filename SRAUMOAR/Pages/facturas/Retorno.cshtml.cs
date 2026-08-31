using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRAUMOAR.Modelos.FacturacionElectronica;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.facturas;

[Authorize(Roles = "Administrador,Administracion,Contabilidad")]
public sealed class RetornoModel : PageModel
{
    private readonly IDteRetornoService _retornos;

    public RetornoModel(IDteRetornoService retornos)
    {
        _retornos = retornos;
    }

    [BindProperty(SupportsGet = true)]
    public int FacturaId { get; set; }

    [BindProperty]
    public Guid OperacionId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Seleccione el tipo de retorno.")]
    public string TipoRetorno { get; set; } = "1";

    [BindProperty]
    [Required(ErrorMessage = "Escriba el motivo del retorno.")]
    [StringLength(1000, ErrorMessage = "El motivo admite hasta 1000 caracteres.")]
    public string Motivo { get; set; } = string.Empty;

    [BindProperty]
    public List<LineaEntrada> Lineas { get; set; } = [];

    public DteRetornoDisponible? Disponible { get; private set; }
    public DteRetornoResultado? Resultado { get; private set; }
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (FacturaId <= 0) return NotFound();

        OperacionId = Guid.NewGuid();

        try
        {
            await CargarDisponibilidadAsync(cancellationToken, conservarEntrada: false);
            return Page();
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (FacturaId <= 0) return NotFound();
        if (OperacionId == Guid.Empty)
            ModelState.AddModelError(nameof(OperacionId), "Falta el identificador de operación. Abra un nuevo formulario.");

        if (TipoRetorno is not ("1" or "2" or "3" or "4"))
            ModelState.AddModelError(nameof(TipoRetorno), "Seleccione un tipo de retorno válido.");

        var seleccionadas = Lineas.Where(x => x.Seleccionada).ToList();
        if (seleccionadas.Count == 0)
            ModelState.AddModelError(string.Empty, "Seleccione al menos una línea para retornar.");

        foreach (var linea in seleccionadas)
        {
            if (linea.Cantidad <= 0)
                ModelState.AddModelError(string.Empty, $"La cantidad del ítem {linea.NumeroItemOriginal} debe ser mayor que cero.");
            // La disponibilidad enviada por el navegador no es autoridad y puede ser cero
            // por la propia reserva del POST que se está reintentando.
            if (linea.PrecioUnitario is <= 0)
                ModelState.AddModelError(string.Empty, $"El precio del ítem {linea.NumeroItemOriginal} debe ser mayor que cero.");
        }

        if (!ModelState.IsValid)
        {
            await CargarDisponibilidadSeguraAsync(cancellationToken);
            return Page();
        }

        try
        {
            var solicitud = new DteRetornoRequest(
                FacturaId,
                TipoRetorno,
                Motivo.Trim(),
                seleccionadas.Select(x => new DteRetornoLineaRequest(
                    x.NumeroItemOriginal,
                    x.Cantidad,
                    x.PrecioUnitario)).ToArray(),
                OperacionId);

            // Esta es la única acción de la página que puede transmitir el ERET.
            Resultado = await _retornos.EmitirAsync(solicitud, cancellationToken);
            if (!Resultado.Aceptado)
                Error = Resultado.Error ?? $"El ERET quedó con estado {Resultado.Estado}.";
            else
            {
                OperacionId = Guid.NewGuid();
                Motivo = string.Empty;
                TipoRetorno = "1";
                Lineas = [];
                Disponible = null;
                // Los tag helpers prefieren ModelState: limpiar también el GUID anterior.
                ModelState.Clear();
            }

            await CargarDisponibilidadSeguraAsync(cancellationToken, conservarEntrada: !Resultado.Aceptado);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            await CargarDisponibilidadSeguraAsync(cancellationToken);
        }

        return Page();
    }

    private async Task CargarDisponibilidadSeguraAsync(CancellationToken cancellationToken, bool conservarEntrada = true)
    {
        try
        {
            await CargarDisponibilidadAsync(cancellationToken, conservarEntrada);
        }
        catch (Exception ex)
        {
            Error ??= ex.Message;
        }
    }

    private async Task CargarDisponibilidadAsync(CancellationToken cancellationToken, bool conservarEntrada)
    {
        Disponible = await _retornos.ObtenerDisponibleAsync(FacturaId, cancellationToken);
        var entradas = conservarEntrada
            ? Lineas.GroupBy(x => x.NumeroItemOriginal).ToDictionary(x => x.Key, x => x.First())
            : new Dictionary<int, LineaEntrada>();

        Lineas = Disponible.Lineas.Select(linea =>
        {
            if (entradas.TryGetValue(linea.NumeroItemOriginal, out var entrada))
            {
                entrada.Codigo = linea.Codigo;
                entrada.Descripcion = linea.Descripcion;
                entrada.CantidadOriginal = linea.CantidadOriginal;
                entrada.CantidadRetornada = linea.CantidadRetornada;
                entrada.CantidadDisponible = linea.CantidadDisponible;
                entrada.ClasificacionFiscal = linea.ClasificacionFiscal;
                if (!entrada.Seleccionada && entrada.PrecioUnitario is null)
                    entrada.PrecioUnitario = linea.PrecioUnitario;
                return entrada;
            }

            return new LineaEntrada
            {
                NumeroItemOriginal = linea.NumeroItemOriginal,
                Codigo = linea.Codigo,
                Descripcion = linea.Descripcion,
                CantidadOriginal = linea.CantidadOriginal,
                CantidadRetornada = linea.CantidadRetornada,
                CantidadDisponible = linea.CantidadDisponible,
                PrecioUnitario = linea.PrecioUnitario,
                ClasificacionFiscal = linea.ClasificacionFiscal
            };
        }).ToList();
    }

    public sealed class LineaEntrada
    {
        public bool Seleccionada { get; set; }
        public int NumeroItemOriginal { get; set; }
        public string? Codigo { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal CantidadOriginal { get; set; }
        public decimal CantidadRetornada { get; set; }
        public decimal CantidadDisponible { get; set; }
        public decimal Cantidad { get; set; }
        public decimal? PrecioUnitario { get; set; }
        public string ClasificacionFiscal { get; set; } = string.Empty;
    }
}
