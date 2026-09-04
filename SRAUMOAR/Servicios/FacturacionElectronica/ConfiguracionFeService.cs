using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using SRAUMOAR.Entidades.FacturacionElectronica;
using SRAUMOAR.Modelos;

namespace SRAUMOAR.Servicios.FacturacionElectronica;

public sealed record ConfiguracionFeActiva(
    ConfiguracionFacturacionElectronica Entidad,
    string? CredencialApi,
    string? PasswordCertificado);

public sealed record ConfiguracionFeInput(
    string AmbienteFiscal,
    bool Habilitado,
    string? UrlAutenticacion,
    string? UrlRecepcion,
    string? UrlAnulacion,
    string? UrlContingencia,
    string? UrlConsulta,
    string? UrlApiExterna,
    string? UrlFirmador,
    string? UsuarioHacienda,
    string? CredencialApi,
    string? PasswordCertificado,
    string? RutaCertificado,
    string CodigoEstablecimientoMh,
    string CodigoEstablecimiento,
    string CodigoPuntoVentaMh,
    string CodigoPuntoVenta,
    bool UsarFirmadorExterno,
    bool UsarApiExterna,
    int TimeoutSegundos,
    int MaxReintentos,
    bool EsAmbienteFiscalActivo,
    string? NitEmisor = null,
    string? NrcEmisor = null,
    string? NombreEmisor = null,
    string? NombreComercial = null,
    string? CodigoActividad = null,
    string? DescripcionActividad = null,
    string? TipoEstablecimiento = null,
    string? TipoEstablecimientoDescripcion = null,
    string? CodigoDepartamento = null,
    string? DepartamentoDescripcion = null,
    string? CodigoMunicipio = null,
    string? MunicipioDescripcion = null,
    string? CodigoDistrito = null,
    string? DistritoDescripcion = null,
    string? ComplementoDireccion = null,
    string? TelefonoEmisor = null,
    string? CorreoEmisor = null);

public interface IConfiguracionFeService
{
    Task<ConfiguracionFeActiva> ObtenerActivaAsync(CancellationToken cancellationToken = default);
    Task<ConfiguracionFeActiva> ObtenerAsync(string ambienteFiscal, CancellationToken cancellationToken = default);
    Task<ConfiguracionFeActiva> ObtenerParaEdicionAsync(string ambienteFiscal, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConfiguracionFeActiva>> ObtenerTodasAsync(CancellationToken cancellationToken = default);
    Task GuardarAsync(ConfiguracionFeInput input, CancellationToken cancellationToken = default);
    Task SeleccionarAmbienteActivoAsync(string ambienteFiscal, CancellationToken cancellationToken = default);
}

public sealed class ConfiguracionFeService : IConfiguracionFeService
{
    private readonly Contexto _contexto;
    private readonly IDataProtector _protector;

    public ConfiguracionFeService(Contexto contexto, IDataProtectionProvider dataProtectionProvider)
    {
        _contexto = contexto;
        _protector = dataProtectionProvider.CreateProtector("SRAUMOAR.FacturacionElectronica.Secretos.v1");
    }

    public async Task<ConfiguracionFeActiva> ObtenerActivaAsync(CancellationToken cancellationToken = default)
    {
        var entidad = await _contexto.ConfiguracionesFacturacionElectronica
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.EsAmbienteFiscalActivo && x.Habilitado, cancellationToken);

        if (entidad is null)
            throw new InvalidOperationException("No existe un ambiente fiscal activo. Configure 00 o 01 antes de transmitir.");

        ValidarDatosEmisor(entidad);
        return Convertir(entidad);
    }

    public async Task<ConfiguracionFeActiva> ObtenerAsync(string ambienteFiscal, CancellationToken cancellationToken = default)
    {
        var ambiente = NormalizarAmbiente(ambienteFiscal);
        var entidad = await _contexto.ConfiguracionesFacturacionElectronica
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AmbienteFiscal == ambiente, cancellationToken);

        if (entidad is null)
            throw new InvalidOperationException($"No existe configuración para el ambiente fiscal {ambiente}.");

        ValidarDatosEmisor(entidad);
        return Convertir(entidad);
    }

    public async Task<ConfiguracionFeActiva> ObtenerParaEdicionAsync(string ambienteFiscal, CancellationToken cancellationToken = default)
    {
        var ambiente = NormalizarAmbiente(ambienteFiscal);
        var entidad = await _contexto.ConfiguracionesFacturacionElectronica
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AmbienteFiscal == ambiente, cancellationToken);

        if (entidad is null)
            throw new InvalidOperationException($"No existe configuraciÃ³n para el ambiente fiscal {ambiente}.");

        return Convertir(entidad);
    }

    public async Task<IReadOnlyList<ConfiguracionFeActiva>> ObtenerTodasAsync(CancellationToken cancellationToken = default)
    {
        var entidades = await _contexto.ConfiguracionesFacturacionElectronica
            .AsNoTracking()
            .OrderBy(x => x.AmbienteFiscal)
            .ToListAsync(cancellationToken);
        return entidades.Select(Convertir).ToArray();
    }

    public async Task GuardarAsync(ConfiguracionFeInput input, CancellationToken cancellationToken = default)
    {
        var ambiente = NormalizarAmbiente(input.AmbienteFiscal);
        var geografia = await _contexto.CatalogosFe.AsNoTracking()
            .Where(x => x.Activo && x.Version == "1.1"
                && (x.CodigoCatalogo == "CAT-012" || x.CodigoCatalogo == "CAT-013" || x.CodigoCatalogo == "CAT-008"))
            .ToListAsync(cancellationToken);
        if (!TerritorioFiscalService.EsDireccionValida(input.CodigoDepartamento, input.CodigoMunicipio, input.CodigoDistrito,
                geografia, geografia, geografia))
            throw new InvalidOperationException("El departamento, municipio y distrito del emisor no forman una ubicación fiscal válida.");
        var codigoDepartamento = TerritorioFiscalService.Normalizar(input.CodigoDepartamento);
        var codigoMunicipio = TerritorioFiscalService.Normalizar(input.CodigoMunicipio);
        var codigoDistrito = TerritorioFiscalService.Normalizar(input.CodigoDistrito);
        var departamentoFiscal = geografia.Single(x => x.CodigoCatalogo == "CAT-012" && x.Codigo == codigoDepartamento);
        var municipioFiscal = geografia.Single(x => x.CodigoCatalogo == "CAT-013" && x.Codigo == codigoMunicipio
            && x.AmbitoDepartamento == codigoDepartamento);
        var distritoFiscal = geografia.Single(x => x.CodigoCatalogo == "CAT-008" && x.Codigo == codigoDistrito
            && x.AmbitoDepartamento == codigoDepartamento && x.AmbitoMunicipio == codigoMunicipio);

        var entidad = await _contexto.ConfiguracionesFacturacionElectronica
            .SingleOrDefaultAsync(x => x.AmbienteFiscal == ambiente, cancellationToken);

        if (entidad is null)
        {
            entidad = new ConfiguracionFacturacionElectronica { AmbienteFiscal = ambiente };
            _contexto.ConfiguracionesFacturacionElectronica.Add(entidad);
        }

        entidad.Habilitado = input.Habilitado;
        entidad.UrlAutenticacion = input.UrlAutenticacion?.Trim();
        entidad.UrlRecepcion = input.UrlRecepcion?.Trim();
        entidad.UrlAnulacion = input.UrlAnulacion?.Trim();
        entidad.UrlContingencia = input.UrlContingencia?.Trim();
        entidad.UrlConsulta = input.UrlConsulta?.Trim();
        entidad.UrlApiExterna = input.UrlApiExterna?.Trim();
        entidad.UrlFirmador = input.UrlFirmador?.Trim();
        entidad.UsuarioHacienda = input.UsuarioHacienda?.Trim();
        entidad.RutaCertificado = input.RutaCertificado?.Trim();
        entidad.CodigoEstablecimientoMh = input.CodigoEstablecimientoMh.Trim();
        entidad.CodigoEstablecimiento = input.CodigoEstablecimiento.Trim();
        entidad.CodigoPuntoVentaMh = input.CodigoPuntoVentaMh.Trim();
        entidad.CodigoPuntoVenta = input.CodigoPuntoVenta.Trim();
        entidad.NitEmisor = input.NitEmisor?.Trim();
        entidad.NrcEmisor = input.NrcEmisor?.Trim();
        entidad.NombreEmisor = input.NombreEmisor?.Trim();
        entidad.NombreComercial = input.NombreComercial?.Trim();
        entidad.CodigoActividad = input.CodigoActividad?.Trim();
        entidad.DescripcionActividad = input.DescripcionActividad?.Trim();
        entidad.TipoEstablecimiento = input.TipoEstablecimiento?.Trim();
        entidad.TipoEstablecimientoDescripcion = input.TipoEstablecimientoDescripcion?.Trim();
        entidad.CodigoDepartamento = codigoDepartamento;
        entidad.DepartamentoDescripcion = departamentoFiscal.Descripcion;
        entidad.CodigoMunicipio = codigoMunicipio;
        entidad.MunicipioDescripcion = municipioFiscal.Descripcion;
        entidad.CodigoDistrito = codigoDistrito;
        entidad.DistritoDescripcion = distritoFiscal.Descripcion;
        entidad.ComplementoDireccion = input.ComplementoDireccion?.Trim();
        entidad.TelefonoEmisor = input.TelefonoEmisor?.Trim();
        entidad.CorreoEmisor = input.CorreoEmisor?.Trim();
        entidad.UsarFirmadorExterno = input.UsarFirmadorExterno;
        entidad.UsarApiExterna = input.UsarApiExterna;
        entidad.TimeoutSegundos = Math.Clamp(input.TimeoutSegundos, 10, 300);
        entidad.MaxReintentos = Math.Clamp(input.MaxReintentos, 1, 10);
        entidad.EsAmbienteFiscalActivo = input.EsAmbienteFiscalActivo;

        if (!string.IsNullOrWhiteSpace(input.CredencialApi))
            entidad.CredencialApiCifrada = _protector.Protect(input.CredencialApi);
        if (!string.IsNullOrWhiteSpace(input.PasswordCertificado))
            entidad.PasswordCertificadoCifrada = _protector.Protect(input.PasswordCertificado);

        if (entidad.EsAmbienteFiscalActivo)
        {
            await _contexto.ConfiguracionesFacturacionElectronica
                .Where(x => x.AmbienteFiscal != ambiente)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.EsAmbienteFiscalActivo, false), cancellationToken);
        }

        entidad.FechaActualizacion = DateTime.UtcNow;
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task SeleccionarAmbienteActivoAsync(string ambienteFiscal, CancellationToken cancellationToken = default)
    {
        var ambiente = NormalizarAmbiente(ambienteFiscal);
        var seleccionado = await _contexto.ConfiguracionesFacturacionElectronica
            .SingleOrDefaultAsync(x => x.AmbienteFiscal == ambiente && x.Habilitado, cancellationToken);

        if (seleccionado is null)
            throw new InvalidOperationException($"El ambiente fiscal {ambiente} no está configurado o habilitado.");

        await _contexto.ConfiguracionesFacturacionElectronica
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.EsAmbienteFiscalActivo, false), cancellationToken);
        seleccionado.EsAmbienteFiscalActivo = true;
        seleccionado.FechaActualizacion = DateTime.UtcNow;
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    public static void ValidarDatosEmisor(ConfiguracionFacturacionElectronica entidad)
    {
        var faltantes = new List<string>();
        AgregarSiFalta(faltantes, entidad.NitEmisor, "NIT del emisor");
        AgregarSiFalta(faltantes, entidad.NrcEmisor, "NRC del emisor");
        AgregarSiFalta(faltantes, entidad.NombreEmisor, "nombre del emisor");
        AgregarSiFalta(faltantes, entidad.CodigoActividad, "código de actividad económica");
        AgregarSiFalta(faltantes, entidad.DescripcionActividad, "descripción de actividad económica");
        AgregarSiFalta(faltantes, entidad.TipoEstablecimiento, "tipo de establecimiento");
        AgregarSiFalta(faltantes, entidad.CodigoDepartamento, "departamento");
        AgregarSiFalta(faltantes, entidad.CodigoMunicipio, "municipio");
        AgregarSiFalta(faltantes, entidad.CodigoDistrito, "distrito");
        AgregarSiFalta(faltantes, entidad.ComplementoDireccion, "complemento de dirección");
        AgregarSiFalta(faltantes, entidad.TelefonoEmisor, "teléfono del emisor");
        AgregarSiFalta(faltantes, entidad.CorreoEmisor, "correo del emisor");

        if (faltantes.Count > 0)
            throw new InvalidOperationException($"Complete los datos fiscales del emisor para el ambiente {entidad.AmbienteFiscal}: {string.Join(", ", faltantes)}.");
    }

    private static void AgregarSiFalta(ICollection<string> faltantes, string? valor, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(valor)) faltantes.Add(descripcion);
    }

    private ConfiguracionFeActiva Convertir(ConfiguracionFacturacionElectronica entidad)
        => new(entidad, Desproteger(entidad.CredencialApiCifrada), Desproteger(entidad.PasswordCertificadoCifrada));

    private string? Desproteger(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        try { return _protector.Unprotect(valor); }
        catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
        {
            throw new InvalidOperationException("No se pudo descifrar una credencial de Facturación Electrónica.", ex);
        }
    }

    public static string NormalizarAmbiente(string ambienteFiscal)
    {
        var ambiente = ambienteFiscal?.Trim();
        return ambiente switch
        {
            "00" => "00",
            "01" => "01",
            _ => throw new ArgumentException("El ambiente fiscal debe ser 00 o 01.", nameof(ambienteFiscal))
        };
    }
}
