using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Accesos
{
    public class ModuloPermiso
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(80)]
        public string Codigo { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string Nombre { get; set; } = string.Empty;

        public int Orden { get; set; }

        public bool Activo { get; set; } = true;

        public virtual ICollection<PermisoModuloRol> PermisosPorRol { get; set; } = new List<PermisoModuloRol>();
    }
}
