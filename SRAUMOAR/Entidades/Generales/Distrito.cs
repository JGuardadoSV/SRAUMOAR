using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Generales
{
    public class Distrito
    {
        public int DistritoId { get; set; }

        [Required(ErrorMessage = "El campo distrito es obligatorio")]
        [Display(Name = "Distrito")]
        public string? NombreDistrito { get; set; }


        //**********************************************************
        [Required(ErrorMessage = "El campo departamento es obligatorio")]
        [Display(Name = "Departamento")]
        public int DepartamentoId { get; set; } // Llave foránea
        public virtual Departamento? Departamento { get; set; } // Propiedad de navegación

        //***********************************************************
        public virtual ICollection<Municipio>? Municipios { get; set; } // Propiedad de navegación

    }
}
