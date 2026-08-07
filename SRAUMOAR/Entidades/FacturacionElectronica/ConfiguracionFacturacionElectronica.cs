using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.FacturacionElectronica;

[Table("ConfiguracionFacturacionElectronica")]
public class ConfiguracionFacturacionElectronica
{
    [Key] public int Id { get; set; }
    [Required, StringLength(2)] public string AmbienteFiscal { get; set; } = "00";
    public bool Habilitado { get; set; } = true;
    public bool EsAmbienteFiscalActivo { get; set; }
    [StringLength(500)] public string? UrlAutenticacion { get; set; }
    [StringLength(500)] public string? UrlRecepcion { get; set; }
    [StringLength(500)] public string? UrlAnulacion { get; set; }
    [StringLength(500)] public string? UrlContingencia { get; set; }
    [StringLength(500)] public string? UrlConsulta { get; set; }
    [StringLength(500)] public string? UrlApiExterna { get; set; }
    [StringLength(500)] public string? UrlFirmador { get; set; }
    [StringLength(200)] public string? UsuarioHacienda { get; set; }
    public string? CredencialApiCifrada { get; set; }
    public string? PasswordCertificadoCifrada { get; set; }
    [StringLength(500)] public string? RutaCertificado { get; set; }
    [StringLength(20)] public string CodigoEstablecimientoMh { get; set; } = "M001";
    [StringLength(20)] public string CodigoEstablecimiento { get; set; } = "M001";
    [StringLength(20)] public string CodigoPuntoVentaMh { get; set; } = "P001";
    [StringLength(20)] public string CodigoPuntoVenta { get; set; } = "P001";

    // Datos fiscales del emisor, independientes por ambiente fiscal.
    [StringLength(20)] public string? NitEmisor { get; set; }
    [StringLength(20)] public string? NrcEmisor { get; set; }
    [StringLength(250)] public string? NombreEmisor { get; set; }
    [StringLength(150)] public string? NombreComercial { get; set; }
    [StringLength(10)] public string? CodigoActividad { get; set; }
    [StringLength(500)] public string? DescripcionActividad { get; set; }
    [StringLength(10)] public string? TipoEstablecimiento { get; set; }
    [StringLength(150)] public string? TipoEstablecimientoDescripcion { get; set; }
    [StringLength(2)] public string? CodigoDepartamento { get; set; }
    [StringLength(150)] public string? DepartamentoDescripcion { get; set; }
    [StringLength(2)] public string? CodigoMunicipio { get; set; }
    [StringLength(150)] public string? MunicipioDescripcion { get; set; }
    [StringLength(2)] public string? CodigoDistrito { get; set; }
    [StringLength(150)] public string? DistritoDescripcion { get; set; }
    [StringLength(200)] public string? ComplementoDireccion { get; set; }
    [StringLength(30)] public string? TelefonoEmisor { get; set; }
    [StringLength(320)] public string? CorreoEmisor { get; set; }

    public bool UsarFirmadorExterno { get; set; }
    public bool UsarApiExterna { get; set; }
    public int TimeoutSegundos { get; set; } = 60;
    public int MaxReintentos { get; set; } = 3;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
