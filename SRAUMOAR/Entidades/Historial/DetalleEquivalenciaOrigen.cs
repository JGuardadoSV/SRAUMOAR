using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Historial
{
    [Table("DetallesEquivalenciaOrigen")]
    public class DetalleEquivalenciaOrigen
    {
        [Key]
        public int DetalleEquivalenciaOrigenId { get; set; }

        [Required]
        public int DetalleEquivalenciaId { get; set; }

        [Required]
        [StringLength(50)]
        public string MateriaOrigenCodigo { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string MateriaOrigenNombre { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal NotaOrigen { get; set; }

        [Required]
        public int uv { get; set; } // Unidades Valorativas (U.V.)

        // Relación
        [ForeignKey("DetalleEquivalenciaId")]
        public virtual DetalleEquivalencia? DetalleEquivalencia { get; set; }
    }
}
