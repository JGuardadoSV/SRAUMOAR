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
        public string Ambiente { get; set; } // 00 = Prueba, 01 = Producción

        public long Correlativo { get; set; }

        public DateTime UltimaActualizacion { get; set; } = DateTime.Now;
    }
}
