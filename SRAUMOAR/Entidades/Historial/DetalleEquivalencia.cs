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

        // Colección de asignaturas de la Universidad de origen (Relación 1 a N)
        public virtual ICollection<DetalleEquivalenciaOrigen> MateriasOrigen { get; set; } = new List<DetalleEquivalenciaOrigen>();

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
