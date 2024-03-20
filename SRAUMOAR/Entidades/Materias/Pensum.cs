

using SRAUMOAR.Entidades.Generales;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Materias
{
    [Table("Pensum")]
    public class Pensum
    {
        public int PensumId { get; set; }

        [Required(ErrorMessage = "El campo nombre del pensum es obligatorio")]
        [Display(Name = "Nombre pensum")]
        public string? NombrePensum { get; set; }

        [Required(ErrorMessage = "El campo código del pensum es obligatorio")]
        [Display(Name = "Código Pensum")]
        public string? CodigoPensum { get; set; }

        [Required(ErrorMessage = "El campo año es obligatorio")]
        [Display(Name = "Año")]
        public int Anio { get; set; }

        [Required(ErrorMessage = "El campo activo es obligatorio")]
        [Display(Name = "Activo / Inactivo")]
        public Boolean Activo { get; set; }

        //lave foranera
        [Required(ErrorMessage = "El campo carrera es obligatorio")]
        [Display(Name = "Carrera a la que pertenece")]
        public int CarreraId { get; set; }
        public virtual Carrera? Carrera { get; set;}

        public virtual ICollection<Materia>? Materias { get; set; } // Propiedad de navegación
    }
}
