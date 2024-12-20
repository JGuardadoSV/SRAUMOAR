using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Becas
{
    public class EntidadBeca
    {
        public int EntidadBecaId { get; set; } // Id de la entidad

        [Required]
        [StringLength(100)]
        [Display(Name = "Nombre de la Entidad")]
        public string? Nombre { get; set; } // Nombre de la entidad

        [Required]
        [StringLength(100)]
        [Display(Name = "Siglas")]
        public string? Siglas { get; set; } // Abreviatura de la entidad

        [StringLength(200)]
        [Display(Name = "Dirección de la Entidad")]
        public string? Direccion { get; set; } // Dirección de la entidad

        [Required]
        [StringLength(100)]
        [Display(Name = "Nombre del Representante")]
        public string? NombreRepresente { get; set; } // Nombre del representante

        [Phone]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; } // Teléfono de contacto

        [Required]
        [Display(Name = "Fecha de Registro")]
        public DateTime FechaDeRegistro { get; set; } // Fecha de registro

        [Required]
        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Correo Electrónico")]
        public string? Email { get; set; } // Correo electrónico

        // Constructor para establecer el valor por defecto de FechaDeRegistro
        public EntidadBeca()
        {
            FechaDeRegistro = DateTime.Now; // Establece la fecha de registro como la fecha actual
        }
    }
}
