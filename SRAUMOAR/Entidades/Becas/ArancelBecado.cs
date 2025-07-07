using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Procesos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Becas
{
    [Table("ArancelesBecados")]
    public class ArancelBecado
    {
        [Key]
        public int ArancelBecadoId { get; set; }

        [Display(Name = "Fecha de Registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [Display(Name = "Estado")]
        public bool Activo { get; set; } = true;

        // Relación con el alumno becado
        [Display(Name = "Alumno Becado")]
        public int BecadosId { get; set; }
        public virtual Becados? Becado { get; set; }

        // Relación con el arancel original
        [Display(Name = "Arancel")]
        public int ArancelId { get; set; }
        public virtual Arancel? Arancel { get; set; }

        // Precio personalizado para este alumno becado
        [Display(Name = "Precio Personalizado")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a 0")]
        public decimal PrecioPersonalizado { get; set; }

        // Porcentaje de descuento aplicado (opcional, para referencia)
        [Display(Name = "Porcentaje de Descuento")]
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100")]
        public decimal PorcentajeDescuento { get; set; }

        // Observaciones sobre el descuento
        [Display(Name = "Observaciones")]
        [StringLength(500)]
        public string? Observaciones { get; set; }

        // Propiedad calculada para el ahorro
        [NotMapped]
        [Display(Name = "Ahorro")]
        public decimal Ahorro => Arancel?.Costo > 0 ? Arancel.Costo - PrecioPersonalizado : 0;

        // Propiedad calculada para el porcentaje de descuento
        [NotMapped]
        [Display(Name = "Descuento Aplicado")]
        public decimal DescuentoAplicado => Arancel?.Costo > 0 ? ((Arancel.Costo - PrecioPersonalizado) / Arancel.Costo) * 100 : 0;
    }
} 