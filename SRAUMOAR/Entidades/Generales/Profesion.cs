using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Generales
{
    [Table("Profesiones")]
    public class Profesion
    {
       
        public int ProfesionId { get; set; }

        [Required(ErrorMessage = "El campo profesión es obligatorio")]
        [Display(Name = "Profesión")]
        public string? NombreProfesion { get; set; }
    }
}
