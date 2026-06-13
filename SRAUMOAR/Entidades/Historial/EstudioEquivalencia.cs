using SRAUMOAR.Entidades.Alumnos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Historial
{
    [Table("EstudiosEquivalencia")]
    public class EstudioEquivalencia
    {
        [Key]
        public int EstudioEquivalenciaId { get; set; }

        [Required]
        public int AlumnoId { get; set; }

        [Required]
        [StringLength(200)]
        public string UniversidadOrigen { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string CarreraOrigen { get; set; } = string.Empty;

        [Required]
        public DateTime FechaEstudio { get; set; } = DateTime.Now;

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Borrador"; // Borrador, Aprobado, Rechazado

        public DateTime? FechaAprobacion { get; set; }

        // Relaciones
        [ForeignKey("AlumnoId")]
        public virtual Alumno? Alumno { get; set; }
        
        public virtual ICollection<DetalleEquivalencia>? Detalles { get; set; }
    }
}
