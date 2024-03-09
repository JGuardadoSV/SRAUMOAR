using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Generales
{
    [Table("Carreras")]
    public class Carrera
    {
        public int CarreraId { get; set; }

        [Required(ErrorMessage = "El campo carrera es obligatorio")]
        [Display(Name = "Nombre de la carrera")]
        public string? NombreCarrera { get; set; }

        [Required(ErrorMessage = "El campo código es obligatorio")]
        [Display(Name = "Código de la carrera")]
        public string? CodigoCarrera { get; set; }

        [Required(ErrorMessage = "El campo duración es obligatorio")]
        [Display(Name = "Años de duración")]
        [Range(1, 5, ErrorMessage = "La duración debe estar entre 1 y 5 años")]
        public int Duracion { get; set; } 

        [Required(ErrorMessage = "El campo ciclos es obligatorio")]
        [Display(Name = "Ciclos de duración")]
        [Range(1, 10, ErrorMessage = "La duración debe estar entre 1 y 10 ciclos")]
        public int Ciclos { get; set; }

        public bool Activa { get; set; } = true;

        //**********************************************************
        [Required(ErrorMessage = "El campo facultad es obligatorio")]
        [Display(Name = "Facultad")]
        public int FacultadId { get; set; } // Llave foránea
        public virtual Facultad? Facultad { get; set; } // Propiedad de navegación

    }
}
