using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.clientesrecurrentes;

[Authorize(Roles = "Administrador,Administracion,Contabilidad")]
public class EditModel : PageModel
{
    private readonly Contexto _context;
    private readonly ICatalogoFeService _catalogos;

    [BindProperty] public ClienteRecurrente Cliente { get; set; } = new();
    public IReadOnlyList<CatalogoFeItem> Actividades { get; private set; } = Array.Empty<CatalogoFeItem>();
    public bool EsNuevo => Cliente.ClienteRecurrenteId == 0;

    public EditModel(Contexto context, ICatalogoFeService catalogos)
    {
        _context = context;
        _catalogos = catalogos;
    }

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        if (id is int clienteId)
        {
            var cliente = await _context.ClientesRecurrentes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClienteRecurrenteId == clienteId, cancellationToken);
            if (cliente is null) return NotFound();
            Cliente = cliente;
        }
        await CargarActividadesAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await CargarActividadesAsync(cancellationToken);
        Cliente.Nit = Regex.Replace(Cliente.Nit ?? string.Empty, @"[^\d]", string.Empty);
        Cliente.Nrc = LimpiarDigitosOpcional(Cliente.Nrc);
        Cliente.Telefono = LimpiarDigitosOpcional(Cliente.Telefono);
        Cliente.Nombre = Cliente.Nombre?.Trim() ?? string.Empty;
        Cliente.Correo = Cliente.Correo?.Trim().ToLowerInvariant();

        ValidarCliente();
        if (!ModelState.IsValid) return Page();

        var entidad = Cliente.ClienteRecurrenteId == 0
            ? new ClienteRecurrente()
            : await _context.ClientesRecurrentes.FirstOrDefaultAsync(x => x.ClienteRecurrenteId == Cliente.ClienteRecurrenteId, cancellationToken);
        if (entidad is null) return NotFound();

        if (Cliente.ClienteRecurrenteId == 0)
            _context.ClientesRecurrentes.Add(entidad);

        entidad.Nit = Cliente.Nit;
        entidad.Nombre = Cliente.Nombre;
        entidad.Nrc = Cliente.Nrc;
        entidad.NombreComercial = Limpiar(Cliente.NombreComercial);
        entidad.CodActividad = Limpiar(Cliente.CodActividad);
        entidad.DescActividad = Limpiar(Cliente.DescActividad);
        entidad.CodigoDepartamento = Limpiar(Cliente.CodigoDepartamento);
        entidad.CodigoMunicipio = Limpiar(Cliente.CodigoMunicipio);
        entidad.CodigoDistrito = Limpiar(Cliente.CodigoDistrito);
        entidad.Complemento = Limpiar(Cliente.Complemento);
        entidad.Telefono = Cliente.Telefono;
        entidad.Correo = Cliente.Correo;
        entidad.CodigoDomicilioFiscal = Cliente.CodigoDomicilioFiscal;
        entidad.GranContribuyente = Cliente.GranContribuyente;
        entidad.RetencionRenta = Cliente.RetencionRenta;
        entidad.FechaActualizacion = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        TempData["Success"] = "Cliente recurrente guardado correctamente.";
        return RedirectToPage("./Index");
    }

    private async Task CargarActividadesAsync(CancellationToken cancellationToken)
        => Actividades = await _catalogos.ObtenerAsync("CAT-019", cancellationToken: cancellationToken);

    private void ValidarCliente()
    {
        if (Cliente.Nit.Length is not (9 or 14) || !Cliente.Nit.All(char.IsDigit))
            ModelState.AddModelError("Cliente.Nit", "El documento debe contener 9 o 14 dígitos, sin guiones.");
        if (string.IsNullOrWhiteSpace(Cliente.Nombre) || Cliente.Nombre.Length > 200)
            ModelState.AddModelError("Cliente.Nombre", "El nombre es obligatorio y no debe superar 200 caracteres.");
        if (!string.IsNullOrWhiteSpace(Cliente.Nrc) && (Cliente.Nrc.Length is < 2 or > 8 || !Cliente.Nrc.All(char.IsDigit)))
            ModelState.AddModelError("Cliente.Nrc", "El NRC debe contener entre 2 y 8 dígitos.");
        if (!string.IsNullOrWhiteSpace(Cliente.Telefono) && (Cliente.Telefono.Length < 8 || Cliente.Telefono.Length > 30 || !Cliente.Telefono.All(char.IsDigit)))
            ModelState.AddModelError("Cliente.Telefono", "El teléfono debe contener entre 8 y 30 dígitos.");
        if (string.IsNullOrWhiteSpace(Cliente.Correo) || Cliente.Correo.Length > 320 || !EsCorreoValido(Cliente.Correo))
            ModelState.AddModelError("Cliente.Correo", "Ingrese un correo válido.");

        var tieneActividad = !string.IsNullOrWhiteSpace(Cliente.CodActividad) || !string.IsNullOrWhiteSpace(Cliente.DescActividad);
        if (tieneActividad && (string.IsNullOrWhiteSpace(Cliente.CodActividad) || string.IsNullOrWhiteSpace(Cliente.DescActividad)))
            ModelState.AddModelError("Cliente.CodActividad", "Seleccione una actividad económica del catálogo.");
        if (!string.IsNullOrWhiteSpace(Cliente.CodActividad) && !Actividades.Any(x => x.Codigo == Cliente.CodActividad))
            ModelState.AddModelError("Cliente.CodActividad", "La actividad económica no pertenece al catálogo de Hacienda.");

        var tieneDireccion = new[] { Cliente.CodigoDepartamento, Cliente.CodigoMunicipio, Cliente.CodigoDistrito, Cliente.Complemento }
            .Any(string.IsNullOrWhiteSpace);
        if (tieneDireccion)
            ModelState.AddModelError(string.Empty, "La dirección debe incluir departamento, municipio, distrito y complemento.");
    }

    private static string? LimpiarDigitosOpcional(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : Regex.Replace(valor, @"[^\d]", string.Empty);

    private static string? Limpiar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static bool EsCorreoValido(string? correo)
    {
        try { return new MailAddress(correo!).Address == correo; }
        catch { return false; }
    }
}
