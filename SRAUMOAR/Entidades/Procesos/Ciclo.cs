using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Procesos
{
    public class Ciclo : IValidatableObject
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
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = false)]
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        public DateTime FechaInicio { get; set; }

        [Required]
        [Display(Name = "Fecha de Fin")]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = false)]
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        public DateTime FechaFin { get; set; }

        [Required]
        [Display(Name = "Correlativo Carnet")]
        public int CorrelativoCarnet { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (FechaInicio > FechaFin)
            {
                yield return new ValidationResult(
                    "La Fecha de Inicio debe ser menor que la Fecha de Fin.",
                    new[] { nameof(FechaInicio), nameof(FechaFin) });
            }

            if (FechaFin < FechaInicio)
            {
                yield return new ValidationResult(
                    "La Fecha de Fin debe ser mayor que la Fecha de Inicio.",
                    new[] { nameof(FechaFin), nameof(FechaInicio) });
            }

            if (FechaInicio.Year != FechaFin.Year)
            {
                yield return new ValidationResult(
                    "La Fecha de Inicio y la Fecha de Fin deben ser del mismo año.",
                    new[] { nameof(FechaInicio), nameof(FechaFin) });
            }
        }

    }
}
