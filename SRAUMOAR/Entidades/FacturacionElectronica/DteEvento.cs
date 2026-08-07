using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SRAUMOAR.Entidades.Colecturia;

namespace SRAUMOAR.Entidades.FacturacionElectronica;

[Table("DteEventos")]
public class DteEvento
{
    [Key] public long Id { get; set; }
    public int FacturaId { get; set; }
    public Factura? Factura { get; set; }
    [Required, StringLength(30)] public string TipoEvento { get; set; } = string.Empty;
    [Required, StringLength(2)] public string AmbienteFiscal { get; set; } = "00";
    [Required, StringLength(36)] public string CodigoEvento { get; set; } = string.Empty;
    [Required] public string JsonEvento { get; set; } = string.Empty;
    [StringLength(100)] public string? SelloRecepcion { get; set; }
    [Required, StringLength(30)] public string Estado { get; set; } = "Preparado";
    public string? Respuesta { get; set; }
    public string? Error { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
