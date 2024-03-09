using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Generales
{
    [Table("Facultades")]
    public class Facultad
    {
        public int FacultadId { get; set; }
        public string? NombreFacultad { get; set; }
        public bool Activa { get; set; } = true;

        //***********************************************************
        public virtual ICollection<Carrera>? Carreras { get; set; } // Propiedad de navegación

    }
}
