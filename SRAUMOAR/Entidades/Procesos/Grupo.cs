using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Materias;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    [Table(name:"grupos")]
    public class Grupo
    {
        public int GrupoId { get; set; }
        public int Limite { get; set; }
        [Display(Name = "Código")]
        public string? Nombre { get; set; }
        public bool Activo { get; set; }

        [Required(ErrorMessage = "La carrera es requerida")]
        [Display(Name = "Carrera")]
        public int CarreraId { get; set; } // Llave foránea
        public virtual Carrera? Carrera { get; set; } // Propiedad de navegación

        [Required(ErrorMessage = "El ciclo es requerido")]
        [Display(Name = "Ciclo")]
        public int CicloId { get; set; } // Llave foránea
        public virtual Ciclo? Ciclo { get; set; } // Propiedad de navegación

        [Required(ErrorMessage = "El docente es requerido")]
        [Display(Name = "Docente encargado")]
        public int DocenteId { get; set; } // Llave foránea
        public virtual Docente? Docente { get; set; } // Propiedad de navegación

        //[Required(ErrorMessage = "El pensum es requerido")]
        //[Display(Name = "Pensum")]
        //[ForeignKey("GruPensum")]
        //[Column("PensumIdG_FK")]
        //public int? PensumId { get; set; } // Llave foránea
        //public virtual Pensum? Pensum { get; set; } // Propiedad de navegación
    }
}
