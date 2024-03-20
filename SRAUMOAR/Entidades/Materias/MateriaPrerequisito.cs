using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Materias
{
    [Table("MateriaPrerequisitos")]
    public class MateriaPrerequisito
    {
        public int MateriaPrerequisitoId { get; set; }
        public int MateriaId { get; set; }
        public Materia? Materia { get; set; }

        public int PrerrequisoMateriaId { get; set; }
        public Materia? PrerrequisoMateria { get; set; }
    }
}
