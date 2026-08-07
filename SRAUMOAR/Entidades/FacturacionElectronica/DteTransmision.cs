using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SRAUMOAR.Entidades.Colecturia;

namespace SRAUMOAR.Entidades.FacturacionElectronica;

[Table("DteTransmisiones")]
public class DteTransmision
{
    [Key] public long Id { get; set; }
    public int? FacturaId { get; set; }
    public Factura? Factura { get; set; }
    [Required, StringLength(20)] public string TipoOperacion { get; set; } = string.Empty;
    [Required, StringLength(2)] public string AmbienteFiscal { get; set; } = "00";
    [Required, StringLength(36)] public string CodigoGeneracion { get; set; } = string.Empty;
    [Required] public string JsonEnviado { get; set; } = string.Empty;
    [StringLength(100)] public string? SelloRecepcion { get; set; }
    public string? Respuesta { get; set; }
    [Required, StringLength(30)] public string Estado { get; set; } = "Preparado";
    public int Intentos { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public string? Error { get; set; }
}
