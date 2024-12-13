using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace SRAUMOAR.Entidades.Accesos
{
    public class Usuario
    {
        [Key]
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es requerido")]
        [StringLength(50)]
        public string? NombreUsuario { get; set; } 

        [Required(ErrorMessage = "La contraseña es requerida")]
        [MinLength(6, ErrorMessage = "La contraseña debe de ser de al menos 6 caracteres")]
        [StringLength(256)]
        public string? Clave { get; set; }

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El email no es válido")]
        [StringLength(100)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "El nivel de acceso es requerido")]
        public int NivelAccesoId { get; set; }

        [DefaultValue(true)]
        public Boolean? Activo { get; set; } = true;

        [ForeignKey("NivelAccesoId")]
        public virtual NivelAcceso?  NivelAcceso { get; set; }
    }
}
