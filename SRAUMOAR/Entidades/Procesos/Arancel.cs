using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    [Table(name:"aranceles")]
    public class Arancel
    {
        [Key]
        public int ArancelId { get; set; }
        public string? Nombre { get; set; }
        public decimal Costo { get; set; }
        public bool Activo { get; set; }

        [Required]
        [Display(Name = "Fecha de Inicio")]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = false)]
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        public DateTime FechaInicio { get; set; }

        [Required]
        [Display(Name = "Fecha de fin")]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = false)]
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        public DateTime FechaFin { get; set; }


        [Required(ErrorMessage = "El ciclo es requerido")]
        [Display(Name = "Ciclo")]
        public int CicloId { get; set; } // Llave foránea
        public virtual Ciclo? Ciclo { get; set; } // Propiedad de navegación

        [DefaultValue(true)]
        public bool Exento { get; set; }


    }
}
