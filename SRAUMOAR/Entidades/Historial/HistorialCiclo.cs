using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Materias;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Historial
{
    [Table("HistorialCiclo")]
    public class HistorialCiclo
    {
        [Key]
        public int HistorialCicloId { get; set; }
        
        [Required]
        public int HistorialAcademicoId { get; set; }
        
        [Required]
        [StringLength(10)]
        public string CicloTexto { get; set; } = string.Empty;
        
        [Required]
        public int PensumId { get; set; }
        
        [ScaffoldColumn(false)]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        
        // Relaciones
        [ForeignKey("HistorialAcademicoId")]
        public virtual HistorialAcademico? HistorialAcademico { get; set; }
        
        [ForeignKey("PensumId")]
        public virtual Pensum? Pensum { get; set; }
        
        public virtual ICollection<HistorialMateria>? MateriasHistorial { get; set; }
    }
}

