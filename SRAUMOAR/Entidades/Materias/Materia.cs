

using SRAUMOAR.Entidades.Generales;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Materias
{

    [Table("Materias")]
    public class Materia
    {
        public int MateriaId { get; set; }

        [Required(ErrorMessage = "El campo nombre de la materia es obligatorio")]
        [Display(Name = "Nombre de la materia")]
        public string? NombreMateria { get; set; }

        [Required(ErrorMessage = "El campo código es obligatorio")]
        [Display(Name = "Código de la materia")]
        public string? CodigoMateria { get; set; }

        [Required(ErrorMessage = "El campo pensum es obligatorio")]
        [Display(Name = "Pensum al que pertenece")]
        public int PensumId { get; set; }
        public virtual Pensum? Pensum { get; set; }

        public ICollection<MateriaPrerequisito>? Prerrequisitos { get; set; }

       
    }


    [Table("MateriaPrerequisitos")]
    public class MateriaPrerequisito
    {
        public int MateriaPrerequisitoId { get; set; }
        public int MateriaId { get; set; }
        public Materia? Materia { get; set; }

        public int PrerrequisoMateriaId { get; set; }
        public Materia? PrerrequisoMateria { get; set; }
    }



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
