using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Becas
{
    //dataannotation nombre de la tabla alumnos becados
    [Table("AlumnosBecados")]
    public class Becados
    {
        
        //key 
        public int BecadosId { get; set; }

        //fecha de registro default now
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        //estado de la beca
        public bool Estado { get; set; }= true;

        //enum Tipo de beca {Completa, Parcial}
        [Display(Name = "Tipo")]
        public int TipoBeca { get; set; } // 1 total 2 parcial


        //relacion con la tabla alumnos
        [Display(Name = "Alumno")]
        public int AlumnoId { get; set; }
        public virtual Alumno? Alumno { get; set; }

        //relacion con la tabla becas
        [Display(Name ="Otorgante")]
        public int EntidadBecaId { get; set; }
        public virtual EntidadBeca? EntidadBeca { get; set; }

        //relacion con la tabla Ciclo
        [Display(Name = "Ciclo")]
        public int CicloId { get; set; }
        public virtual Ciclo? Ciclo { get; set; }


    }
}
