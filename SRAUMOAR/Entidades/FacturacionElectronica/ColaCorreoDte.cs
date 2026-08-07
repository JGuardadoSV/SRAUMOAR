using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SRAUMOAR.Entidades.Colecturia;

namespace SRAUMOAR.Entidades.FacturacionElectronica;

[Table("ColaCorreosDte")]
public class ColaCorreoDte
{
    [Key] public long Id { get; set; }
    public int? FacturaId { get; set; }
    public Factura? Factura { get; set; }
    [Required, StringLength(30)] public string TipoCorreo { get; set; } = "Dte";
    [Required, StringLength(320)] public string Destinatario { get; set; } = string.Empty;
    [StringLength(1000)] public string? Copias { get; set; }
    [Required, StringLength(250)] public string Asunto { get; set; } = string.Empty;
    [Required] public string CuerpoHtml { get; set; } = string.Empty;
    [Required] public string JsonAdjunto { get; set; } = string.Empty;
    [Required, StringLength(250)] public string NombreJson { get; set; } = string.Empty;
    public byte[]? PdfAdjunto { get; set; }
    [StringLength(250)] public string? NombrePdf { get; set; }
    [StringLength(36)] public string? CodigoGeneracion { get; set; }
    [StringLength(100)] public string? Sello { get; set; }
    [Required, StringLength(30)] public string Estado { get; set; } = "Pendiente";
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? ProximoIntento { get; set; }
    public DateTime? ProcesadoEn { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    [Required, StringLength(100)] public string ClaveIdempotencia { get; set; } = string.Empty;
}
