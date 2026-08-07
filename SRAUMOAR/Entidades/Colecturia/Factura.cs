using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Colecturia
{
    [Table("Factura")]
    public class Factura
    {
        [Key]
        public int FacturaId { get; set; }

        public string? JsonDte { get; set; }

        public bool Anulada { get; set; } = false;

        public string? SelloRecepcion { get; set; }

        public string? JsonAnulacion { get; set; }

        public string? SelloAnulacion { get; set; }

        public DateTime? FechaAnulacion { get; set; }

        public int? FacturaOriginalId { get; set; }

        public Factura? FacturaOriginal { get; set; }

        public ICollection<Factura> NotasDerivadas { get; set; } = new List<Factura>();

        [StringLength(500)]
        public string? MotivoNota { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MontoNota { get; set; }

        [StringLength(100)]
        public string? ClaveIdempotencia { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        [Required]
        [StringLength(100)]
        public string CodigoGeneracion { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string NumeroControl { get; set; } = string.Empty;

        [Required]
        public int TipoDTE { get; set; }

        [StringLength(2)]
        public string? TipoDteCodigo { get; set; }

        [StringLength(2)]
        public string? AmbienteFiscal { get; set; }

        [StringLength(30)]
        public string EstadoTransmision { get; set; } = "Preparado";

        public int IntentosTransmision { get; set; }

        public string? RespuestaHacienda { get; set; }

        public DateTime? FechaAceptacion { get; set; }

        [StringLength(320)]
        public string? CorreoReceptor { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGravado { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalExento { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPagar { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalIva { get; set; }
    }
}
