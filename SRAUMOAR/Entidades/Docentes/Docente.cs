using SRAUMOAR.Entidades.Generales;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using SRAUMOAR.Entidades.Accesos;

namespace SRAUMOAR.Entidades.Docentes
{
    public class Docente
    {
        public int DocenteId { get; set; }

        [ScaffoldColumn(false)]
        public DateTime FechaDeRegistro { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [DisplayName("Nombres")]
        public string Nombres { get; set; }

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [DisplayName("Apellidos")]
        public string Apellidos { get; set; }

        [Required(ErrorMessage = "El DUI es obligatorio")]
        [RegularExpression(@"^\d{8}-\d$", ErrorMessage = "El formato del DUI no es válido. Debe ser 00000000-0")]
        [DisplayName("DUI")]
        [UIHint("Dui", "Placeholder = 00000000-0")]
        public string Dui { get; set; }

        [Required(ErrorMessage = "La fecha de nacimiento es obligatoria")]
        [DisplayName("Fecha de Nacimiento")]
        [DisplayFormat(ApplyFormatInEditMode = false, DataFormatString = "{0:dd/MM/yyyy}")]
        [DataType(DataType.Date)]
        public DateTime FechaDeNacimiento { get; set; }

        [Required(ErrorMessage = "La dirección es obligatoria")]
        [DisplayName("Dirección")]
        public string Direccion { get; set; }

        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress(ErrorMessage = "El correo electrónico no es válido")]
        [DisplayName("Correo Electrónico")]
        public string Email { get; set; }

        [Required(ErrorMessage = "La profesión es obligatoria")]
        [DisplayName("Profesión")]
        public int ProfesionId { get; set; }

        [ForeignKey("ProfesionId")]
        [DisplayName("Profesión")]
        public virtual Profesion? Profesion { get; set; }

        [Required(ErrorMessage = "El campo género  es obligatorio")]
        [Display(Name = "Género")]
        public int Genero { get; set; }

        // Relación con Usuario
        public int? UsuarioId { get; set; }
        [ForeignKey("UsuarioId")]
        public virtual Usuario? Usuario { get; set; }   
    }
}
