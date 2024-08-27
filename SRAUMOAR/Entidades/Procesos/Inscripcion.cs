using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    [Table(name:"Inscripciones")]
    public class Inscripcion
    {
        public Inscripcion()
        {
            this.Activa = true;
            this.Fecha=DateTime.Now;
        }


        [Key]
        public int InscripcionId { get; set; }
        public DateTime Fecha { get; set; }
        public Boolean Activa { get; set; }

        //**********************************************************
        [Required(ErrorMessage = "El Alumno es requerido")]
        [Display(Name = "Alumno")]
        public int AlumnoId { get; set; } // Llave foránea
        public virtual Alumno? Alumno { get; set; } // Propiedad de navegación

        //**********************************************************
        [Required(ErrorMessage = "El ciclo es requerido")]
        [Display(Name = "Ciclo")]
        public int CicloId { get; set; } // Llave foránea
        public virtual Ciclo? Ciclo { get; set; } // Propiedad de navegación

        //**********************************************************
        [Required(ErrorMessage = "La Carrera es requerida")]
        [Display(Name = "Carrera")]
        public int CarreraId { get; set; } // Llave foránea
        public virtual Carrera? Carrera { get; set; } // Propiedad de navegación
    }
}
