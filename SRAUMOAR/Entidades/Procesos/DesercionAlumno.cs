using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SRAUMOAR.Entidades.Alumnos;

namespace SRAUMOAR.Entidades.Procesos
{
    public class DesercionAlumno
    {
        [Key]
        public int DesercionAlumnoId { get; set; }

        [Required]
        [Display(Name = "Alumno")]
        public int AlumnoId { get; set; }

        [ForeignKey(nameof(AlumnoId))]
        public virtual Alumno? Alumno { get; set; }

        [Required]
        [Display(Name = "Ciclo")]
        public int CicloId { get; set; }

        [ForeignKey(nameof(CicloId))]
        public virtual Ciclo? Ciclo { get; set; }

        [Required]
        [Display(Name = "Causa de deserción")]
        public int CausaDesercionId { get; set; }

        [ForeignKey(nameof(CausaDesercionId))]
        public virtual CausaDesercion? CausaDesercion { get; set; }

        [StringLength(500)]
        [Display(Name = "Observación")]
        public string? Observacion { get; set; }

        [Display(Name = "Fecha de registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
