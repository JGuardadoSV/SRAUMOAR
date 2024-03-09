using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Generales
{
    public class Municipio
    {
        public int MunicipioId { get; set; }

        [Required(ErrorMessage = "El campo municipio es obligatorio")]
        [Display(Name = "Municipio")]
        public string? NombreMunicipio { get; set; }

        //**********************************************************
        [Required(ErrorMessage = "El campo distrito es obligatorio")]
        [Display(Name = "Distrito")]
        public int DistritoId { get; set; } // Llave foránea
        public virtual Distrito? Distrito { get; set; } // Propiedad de navegación
    }
}
