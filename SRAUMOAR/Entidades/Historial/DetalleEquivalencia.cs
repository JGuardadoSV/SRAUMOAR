using SRAUMOAR.Entidades.Materias;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Historial
{
    [Table("DetallesEquivalencia")]
    public class DetalleEquivalencia
    {
        [Key]
        public int DetalleEquivalenciaId { get; set; }

        [Required]
        public int EstudioEquivalenciaId { get; set; }

        // Asignatura de la Universidad de origen
        [Required]
        [StringLength(50)]
        public string MateriaOrigenCodigo { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string MateriaOrigenNombre { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal NotaOrigen { get; set; }

        // Asignatura homóloga en UMOAR
        [Required]
        public int MateriaDestinoId { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal NotaEquivalencia { get; set; } // Calificación asignada en UMOAR (>= 7.0)

        // Relaciones
        [ForeignKey("EstudioEquivalenciaId")]
        public virtual EstudioEquivalencia? EstudioEquivalencia { get; set; }

        [ForeignKey("MateriaDestinoId")]
        public virtual Materia? MateriaDestino { get; set; }
    }
}
