using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Generales
{
    public class CodigoActividadEconomica
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El campo código es obligatorio")]
        [Display(Name = "Código")]
        [StringLength(20, ErrorMessage = "El código no puede exceder los 20 caracteres")]
        public string? Codigo { get; set; }

        [Required(ErrorMessage = "El campo descripción es obligatorio")]
        [Display(Name = "Descripción")]
        [StringLength(500, ErrorMessage = "La descripción no puede exceder los 500 caracteres")]
        public string? Descripcion { get; set; }
    }
}
