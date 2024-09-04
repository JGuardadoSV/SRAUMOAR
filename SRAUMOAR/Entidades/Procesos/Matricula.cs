using SRAUMOAR.Entidades.Alumnos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    //Nombre la tabla Matricula
    [Table("Matriculas")]
    public class Matricula
    {
        //Primary Key data anotation
        [Key]
        public int MatriculaId { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; }
        //Fecha de cancelacion, por defecto nula
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        [Display(Name = "Fecha de cancelación")]
        public DateTime? FechaCancelacion { get; set; }
        //observaciones
        public string? Observaciones { get; set; }
        public bool Activa { get; set; }
        
        //Hay una entidad llamada ciclo, requiero una FK con la entidad Ciclo
        [ForeignKey("CicloId")]
        public int CicloId { get; set; }
        public virtual Ciclo? Ciclo { get; set; }

        //Hay una entidad llamada Alumno, requiero una FK con la entidad Alumno
        [ForeignKey("AlumnoId")]
        public int AlumnoId { get; set; }
        public virtual Alumno? Alumno { get; set; }

    }

}
