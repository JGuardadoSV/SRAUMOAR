using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Generales
{
    public class Departamento
    {
        public int DepartamentoId { get; set; }

        [Required(ErrorMessage = "El campo departamento es obligatorio")]
        [Display(Name = "Departamento")]
        public string? NombreDepartamento { get; set; }


        //**********************************************************
        public virtual ICollection<Distrito>? Distritos { get; set; } // Propiedad de navegación


    }
}
