using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Servicios.FacturacionElectronica;

namespace SRAUMOAR.Pages.administracion.facturacion;

[Authorize(Roles = "Administrador,Administracion")]
public class IndexModel : PageModel
{
    private readonly IConfiguracionFeService _configuracion;
    private readonly ICertificadoStorageService _certificados;
    private readonly ISmtpConfigurationService _smtp;
    private readonly ICatalogoFeService _catalogos;
    private readonly IOptions<EmisorConfig> _emisor;

    public IndexModel(IConfiguracionFeService configuracion, ICertificadoStorageService certificados, ISmtpConfigurationService smtp, ICatalogoFeService catalogos, IOptions<EmisorConfig> emisor)
    {
        _configuracion = configuracion;
        _certificados = certificados;
        _smtp = smtp;
        _catalogos = catalogos;
        _emisor = emisor;
    }

    [BindProperty] public AmbienteForm Form00 { get; set; } = new() { AmbienteFiscal = "00" };
    [BindProperty] public AmbienteForm Form01 { get; set; } = new() { AmbienteFiscal = "01" };
    [BindProperty] public SmtpForm Smtp { get; set; } = new();
    public string? AmbienteActivo { get; private set; }
    public string? Mensaje { get; private set; }
    public string? Error { get; private set; }
    public IReadOnlyList<CatalogoFeItem> CatalogoDepartamentos { get; private set; } = Array.Empty<CatalogoFeItem>();
    public IReadOnlyList<CatalogoFeItem> CatalogoMunicipios { get; private set; } = Array.Empty<CatalogoFeItem>();
    public IReadOnlyList<CatalogoFeItem> CatalogoDistritos { get; private set; } = Array.Empty<CatalogoFeItem>();
    public IReadOnlyList<CatalogoFeItem> CatalogoActividades { get; private set; } = Array.Empty<CatalogoFeItem>();
    public IReadOnlyList<CatalogoFeItem> CatalogoEstablecimientos { get; private set; } = Array.Empty<CatalogoFeItem>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await CargarAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostGuardarAmbienteAsync(string ambienteFiscal, IFormFile? certificado, CancellationToken cancellationToken)
    {
        var ambiente = ConfiguracionFeService.NormalizarAmbiente(ambienteFiscal);
        var form = ambiente == "00" ? Form00 : Form01;
        if (!ModelState.IsValid) { await CargarAsync(cancellationToken); return Page(); }

        try
        {
            string? ruta = null;
            try
            {
                var actual = await _configuracion.ObtenerParaEdicionAsync(ambiente, cancellationToken);
                ruta = actual.Entidad.RutaCertificado;
            }
            catch (InvalidOperationException)
            {
                // Primera configuración: la migración deja filas base sin secretos.
            }
            if (certificado is { Length: > 0 })
                ruta = await _certificados.GuardarAsync(ambiente, form.NitEmisor ?? _emisor.Value.NIT, certificado.OpenReadStream(), certificado.FileName, cancellationToken);

            await _configuracion.GuardarAsync(new ConfiguracionFeInput(
                ambiente, form.Habilitado, form.UrlAutenticacion, form.UrlRecepcion, form.UrlAnulacion, form.UrlContingencia,
                form.UrlConsulta, form.UrlApiExterna, form.UrlFirmador, form.UsuarioHacienda, form.CredencialApi,
                form.PasswordCertificado, ruta, form.CodigoEstablecimientoMh, form.CodigoEstablecimiento,
                form.CodigoPuntoVentaMh, form.CodigoPuntoVenta, form.UsarFirmadorExterno, form.UsarApiExterna,
                form.TimeoutSegundos, form.MaxReintentos, form.EsAmbienteFiscalActivo,
                form.NitEmisor, form.NrcEmisor, form.NombreEmisor, form.NombreComercial,
                form.CodigoActividad, form.DescripcionActividad, form.TipoEstablecimiento,
                form.TipoEstablecimientoDescripcion, form.CodigoDepartamento, form.DepartamentoDescripcion,
                form.CodigoMunicipio, form.MunicipioDescripcion, form.CodigoDistrito, form.DistritoDescripcion,
                form.ComplementoDireccion, form.TelefonoEmisor, form.CorreoEmisor), cancellationToken);
            Mensaje = $"Configuración del ambiente fiscal {ambiente} guardada.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        await CargarAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostActivarAmbienteAsync(string ambienteFiscal, CancellationToken cancellationToken)
    {
        try
        {
            await _configuracion.SeleccionarAmbienteActivoAsync(ambienteFiscal, cancellationToken);
            Mensaje = $"El ambiente fiscal activo ahora es {ConfiguracionFeService.NormalizarAmbiente(ambienteFiscal)}.";
        }
        catch (Exception ex) { Error = ex.Message; }
        await CargarAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostGuardarSmtpAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _smtp.GuardarAsync(new SmtpConfigurationInput(Smtp.Servidor, Smtp.Puerto, Smtp.Usuario, Smtp.Password, Smtp.EmailRemitente, Smtp.NombreRemitente, Smtp.CopiaOculta, Smtp.UsarSsl, Smtp.UsarTls, Smtp.MaxReintentos), cancellationToken);
            Mensaje = "Configuración SMTP guardada.";
        }
        catch (Exception ex) { Error = ex.Message; }
        await CargarAsync(cancellationToken);
        return Page();
    }

    private async Task CargarAsync(CancellationToken cancellationToken)
    {
        CatalogoDepartamentos = await _catalogos.ObtenerAsync("CAT-012", "1.1", cancellationToken);
        CatalogoMunicipios = await _catalogos.ObtenerAsync("CAT-013", "1.1", cancellationToken);
        CatalogoDistritos = await _catalogos.ObtenerAsync("CAT-008", "1.1", cancellationToken);
        CatalogoActividades = await _catalogos.ObtenerAsync("CAT-019", "1.1", cancellationToken);
        CatalogoEstablecimientos = await _catalogos.ObtenerAsync("CAT-009", "1.1", cancellationToken);

        var ambientes = await _configuracion.ObtenerTodasAsync(cancellationToken);
        foreach (var ambiente in ambientes)
        {
            var form = ambiente.Entidad.AmbienteFiscal == "00" ? Form00 : Form01;
            form.AmbienteFiscal = ambiente.Entidad.AmbienteFiscal;
            form.Habilitado = ambiente.Entidad.Habilitado;
            form.EsAmbienteFiscalActivo = ambiente.Entidad.EsAmbienteFiscalActivo;
            form.UrlAutenticacion = ambiente.Entidad.UrlAutenticacion;
            form.UrlRecepcion = ambiente.Entidad.UrlRecepcion;
            form.UrlAnulacion = ambiente.Entidad.UrlAnulacion;
            form.UrlContingencia = ambiente.Entidad.UrlContingencia;
            form.UrlConsulta = ambiente.Entidad.UrlConsulta;
            form.UrlApiExterna = ambiente.Entidad.UrlApiExterna;
            form.UrlFirmador = ambiente.Entidad.UrlFirmador;
            form.UsuarioHacienda = ambiente.Entidad.UsuarioHacienda;
            form.CodigoEstablecimientoMh = ambiente.Entidad.CodigoEstablecimientoMh;
            form.CodigoEstablecimiento = ambiente.Entidad.CodigoEstablecimiento;
            form.CodigoPuntoVentaMh = ambiente.Entidad.CodigoPuntoVentaMh;
            form.CodigoPuntoVenta = ambiente.Entidad.CodigoPuntoVenta;
            form.NitEmisor = ambiente.Entidad.NitEmisor;
            form.NrcEmisor = ambiente.Entidad.NrcEmisor;
            form.NombreEmisor = ambiente.Entidad.NombreEmisor;
            form.NombreComercial = ambiente.Entidad.NombreComercial;
            form.CodigoActividad = ambiente.Entidad.CodigoActividad;
            form.DescripcionActividad = ambiente.Entidad.DescripcionActividad;
            form.TipoEstablecimiento = ambiente.Entidad.TipoEstablecimiento;
            form.TipoEstablecimientoDescripcion = ambiente.Entidad.TipoEstablecimientoDescripcion;
            form.CodigoDepartamento = ambiente.Entidad.CodigoDepartamento;
            form.DepartamentoDescripcion = ambiente.Entidad.DepartamentoDescripcion;
            form.CodigoMunicipio = ambiente.Entidad.CodigoMunicipio;
            form.MunicipioDescripcion = ambiente.Entidad.MunicipioDescripcion;
            form.CodigoDistrito = ambiente.Entidad.CodigoDistrito;
            form.DistritoDescripcion = ambiente.Entidad.DistritoDescripcion;
            form.ComplementoDireccion = ambiente.Entidad.ComplementoDireccion;
            form.TelefonoEmisor = ambiente.Entidad.TelefonoEmisor;
            form.CorreoEmisor = ambiente.Entidad.CorreoEmisor;
            form.UsarFirmadorExterno = ambiente.Entidad.UsarFirmadorExterno;
            form.UsarApiExterna = ambiente.Entidad.UsarApiExterna;
            form.TimeoutSegundos = ambiente.Entidad.TimeoutSegundos;
            form.MaxReintentos = ambiente.Entidad.MaxReintentos;
            form.TieneCertificado = !string.IsNullOrWhiteSpace(ambiente.Entidad.RutaCertificado);
            if (ambiente.Entidad.EsAmbienteFiscalActivo) AmbienteActivo = ambiente.Entidad.AmbienteFiscal;
        }

        var smtp = await _smtp.ObtenerAsync(cancellationToken);
        if (smtp.Entidad is not null)
        {
            Smtp.Servidor = smtp.Entidad.Servidor;
            Smtp.Puerto = smtp.Entidad.Puerto;
            Smtp.Usuario = smtp.Entidad.Usuario;
            Smtp.EmailRemitente = smtp.Entidad.EmailRemitente;
            Smtp.NombreRemitente = smtp.Entidad.NombreRemitente;
            Smtp.CopiaOculta = smtp.Entidad.CopiaOculta;
            Smtp.UsarSsl = smtp.Entidad.UsarSsl;
            Smtp.UsarTls = smtp.Entidad.UsarTls;
            Smtp.MaxReintentos = smtp.Entidad.MaxReintentos;
            Smtp.TienePassword = smtp.TienePassword;
        }
    }

    public class AmbienteForm
    {
        public string AmbienteFiscal { get; set; } = string.Empty;
        public bool Habilitado { get; set; } = true;
        public bool EsAmbienteFiscalActivo { get; set; }
        public string? UrlAutenticacion { get; set; }
        public string? UrlRecepcion { get; set; }
        public string? UrlAnulacion { get; set; }
        public string? UrlContingencia { get; set; }
        public string? UrlConsulta { get; set; }
        public string? UrlApiExterna { get; set; }
        public string? UrlFirmador { get; set; }
        public string? UsuarioHacienda { get; set; }
        public string? CredencialApi { get; set; }
        public string? PasswordCertificado { get; set; }
        public string? CodigoEstablecimientoMh { get; set; } = "M001";
        public string? CodigoEstablecimiento { get; set; } = "M001";
        public string? CodigoPuntoVentaMh { get; set; } = "P001";
        public string? CodigoPuntoVenta { get; set; } = "P001";
        public string? NitEmisor { get; set; }
        public string? NrcEmisor { get; set; }
        public string? NombreEmisor { get; set; }
        public string? NombreComercial { get; set; }
        public string? CodigoActividad { get; set; }
        public string? DescripcionActividad { get; set; }
        public string? TipoEstablecimiento { get; set; }
        public string? TipoEstablecimientoDescripcion { get; set; }
        public string? CodigoDepartamento { get; set; }
        public string? DepartamentoDescripcion { get; set; }
        public string? CodigoMunicipio { get; set; }
        public string? MunicipioDescripcion { get; set; }
        public string? CodigoDistrito { get; set; }
        public string? DistritoDescripcion { get; set; }
        public string? ComplementoDireccion { get; set; }
        public string? TelefonoEmisor { get; set; }
        public string? CorreoEmisor { get; set; }
        public bool UsarFirmadorExterno { get; set; }
        public bool UsarApiExterna { get; set; }
        public int TimeoutSegundos { get; set; } = 60;
        public int MaxReintentos { get; set; } = 3;
        public bool TieneCertificado { get; set; }
    }

    public class SmtpForm
    {
        public string Servidor { get; set; } = string.Empty;
        public int Puerto { get; set; } = 587;
        public string? Usuario { get; set; }
        public string? Password { get; set; }
        public string EmailRemitente { get; set; } = string.Empty;
        public string? NombreRemitente { get; set; }
        public string? CopiaOculta { get; set; }
        public bool UsarSsl { get; set; }
        public bool UsarTls { get; set; } = true;
        public int MaxReintentos { get; set; } = 5;
        public bool TienePassword { get; set; }
    }
}
