using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Accesos
{

    public class NivelAcceso
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del nivel de acceso es requerido")]
        [StringLength(50)]
        public string? Nombre { get; set; }

        public virtual ICollection<Usuario>? Usuarios { get; set; }
    }
}
