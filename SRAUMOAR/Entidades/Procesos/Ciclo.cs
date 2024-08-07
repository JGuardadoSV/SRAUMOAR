using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Procesos
{
    public class Ciclo
    {
       public Ciclo()
        {
            this.FechaRegistro=DateTime.Now;
            this.Activo = true;
        }

        [Key]
        public int Id { get; set; }

        [ScaffoldColumn(false)]
        public DateTime FechaRegistro { get; set; }

        [Required]
        public bool Activo { get; set; }

        [Required]
        [Display(Name = "Número de Ciclo")]
        public int NCiclo { get; set; }

        [Required]
        [Display(Name = "Año")]
        public int anio { get; set; }

        [Required]
        [Display(Name = "Fecha de Inicio")]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "date")]
        public DateTime FechaInicio { get; set; }

        [Required]
        [Display(Name = "Fecha de Fin")]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = true)]
        [Column(TypeName = "date")]
        public DateTime FechaFin { get; set; }

        
    }
}
