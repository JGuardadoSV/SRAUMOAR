using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Historial
{
    [Table("HistorialAcademico")]
    public class HistorialAcademico
    {
        [Key]
        public int HistorialAcademicoId { get; set; }
        
        [Required]
        public int AlumnoId { get; set; }
        
        // Puede ser nulo para historiales antiguos sin carrera asignada
        public int? CarreraId { get; set; }
        
        [ScaffoldColumn(false)]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        
        // Relaciones
        [ForeignKey("AlumnoId")]
        public virtual Alumno? Alumno { get; set; }
        
        [ForeignKey("CarreraId")]
        public virtual Carrera? Carrera { get; set; }
        
        public virtual ICollection<HistorialCiclo>? CiclosHistorial { get; set; }
    }
}
