using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Procesos
{
    [Table("DteCorrelativos")]
    public class DteCorrelativo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(2)]
        public string TipoDocumento { get; set; } // 01, 03, 05, 11, 15

        [Required]
        [StringLength(2)]
        public string Ambiente { get; set; } // Ambiente fiscal 00 o 01

        [Required]
        [StringLength(20)]
        public string Establecimiento { get; set; } = "M001";

        [Required]
        [StringLength(20)]
        public string PuntoVenta { get; set; } = "P001";

        public long Correlativo { get; set; }

        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;
    }
}
