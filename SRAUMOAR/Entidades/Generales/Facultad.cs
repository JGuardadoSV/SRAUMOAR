using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Generales
{
    [Table("Facultades")]
    public class Facultad
    {
        public int FacultadId { get; set; }
        [DisplayName(displayName:"Nombre de la facultad")]
        [Required(ErrorMessage = "El campo Nombre de facultad es obligatorio")]
        public string? NombreFacultad { get; set; }
        [DisplayName(displayName:"Código de la facultad")]
        [Required(ErrorMessage = "El campo Código de facultad es obligatorio,es para fines de generar el carnet de estudiante")]
        public string? CodigoFacultad { get; set; }
        public bool Activa { get; set; } = true;

        //***********************************************************
        public virtual ICollection<Carrera>? Carreras { get; set; } // Propiedad de navegación

    }
}
