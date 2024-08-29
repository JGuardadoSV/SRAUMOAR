using SRAUMOAR.Entidades.Materias;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    [Table(name:"GruposMaterias")]
    public class MateriasGrupo
    {
        public int MateriasGrupoId { get; set; }

        public string? Aula { get; set; }
        public string? Horario { get; set; }

        [Required(ErrorMessage = "La Materia es requerida")]
        [Display(Name = "Materia")]
        public int MateriaId { get; set; } // Llave foránea
        public virtual Materia? Materia { get; set; } // Propiedad de navegación

        [Required(ErrorMessage = "El grupo es requerido")]
        [Display(Name = "Grupo")]
        public int GrupoId { get; set; } // Llave foránea
        public virtual Grupo? Grupo { get; set; } // Propiedad de navegación
    }
}
