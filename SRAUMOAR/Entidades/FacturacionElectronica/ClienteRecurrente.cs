using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.FacturacionElectronica;

public class ClienteRecurrente
{
    public int ClienteRecurrenteId { get; set; }

    [Required, StringLength(200)] public string Nombre { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Nit { get; set; } = string.Empty;
    [StringLength(20)] public string? Nrc { get; set; }
    [StringLength(200)] public string? NombreComercial { get; set; }
    [StringLength(20)] public string? CodActividad { get; set; }
    [StringLength(250)] public string? DescActividad { get; set; }
    [StringLength(10)] public string? CodigoDepartamento { get; set; }
    [StringLength(10)] public string? CodigoMunicipio { get; set; }
    [StringLength(10)] public string? CodigoDistrito { get; set; }
    [StringLength(250)] public string? Complemento { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [StringLength(320)] public string? Correo { get; set; }
    public int? CodigoDomicilioFiscal { get; set; }
    public bool GranContribuyente { get; set; }
    public bool RetencionRenta { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
