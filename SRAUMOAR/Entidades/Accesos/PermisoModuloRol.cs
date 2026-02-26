using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Accesos
{
    public class PermisoModuloRol
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ModuloPermisoId { get; set; }

        [Required]
        public int NivelAccesoId { get; set; }

        public bool PuedeVer { get; set; }

        [ForeignKey(nameof(ModuloPermisoId))]
        public virtual ModuloPermiso? ModuloPermiso { get; set; }

        [ForeignKey(nameof(NivelAccesoId))]
        public virtual NivelAcceso? NivelAcceso { get; set; }
    }
}
