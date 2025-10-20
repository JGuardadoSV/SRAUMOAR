using SRAUMOAR.Entidades.Materias;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Historial
{
    [Table("HistorialMateria")]
    public class HistorialMateria
    {
        [Key]
        public int HistorialMateriaId { get; set; }
        
        [Required]
        public int HistorialCicloId { get; set; }
        
        [Required]
        public int MateriaId { get; set; }
        
        // Notas individuales
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Nota1 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Nota2 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Nota3 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Nota4 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Nota5 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Nota6 { get; set; }
        
        // Promedio
        [Range(0, 10, ErrorMessage = "El promedio debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Promedio { get; set; }
        
        public bool Aprobada { get; set; }
        
        public bool Equivalencia { get; set; }
        
        public bool ExamenSuficiencia { get; set; }
        
        [ScaffoldColumn(false)]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        
        // Relaciones
        [ForeignKey("HistorialCicloId")]
        public virtual HistorialCiclo? HistorialCiclo { get; set; }
        
        [ForeignKey("MateriaId")]
        public virtual Materia? Materia { get; set; }
    }
}

