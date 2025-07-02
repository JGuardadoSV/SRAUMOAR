using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    [Table(name: "aranceles")]
    public class Arancel
    {
        [Key]
        public int ArancelId { get; set; }
        public string? Nombre { get; set; }
        public decimal Costo { get; set; }
        public bool Activo { get; set; }

        [Display(Name = "Fecha de Inicio")]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = false)]
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        public DateTime? FechaInicio { get; set; }

        [Display(Name = "Fecha de fin")]
        [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}", ApplyFormatInEditMode = false)]
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        public DateTime? FechaFin { get; set; }

        [Display(Name = "Ciclo")]
        public int? CicloId { get; set; }
        public virtual Ciclo? Ciclo { get; set; }

        [DefaultValue(true)]
        public bool Exento { get; set; }

        [DefaultValue(false)]
        public bool Obligatorio { get; set; }

        // Campo para valor de mora
        [Display(Name = "Valor de Mora")]
        [Column(TypeName = "decimal(18,2)")]
        [DefaultValue(0)]
        public decimal ValorMora { get; set; }

        // Propiedad calculada para saber si está vencido
        [NotMapped]
        [Display(Name = "Está Vencido")]
        public bool EstaVencido => FechaFin.HasValue && FechaFin.Value.Date < DateTime.Now.Date;

        // Total con mora incluida
        [NotMapped]
        [Display(Name = "Total con Mora")]
        public decimal TotalConMora => Costo + (EstaVencido ? ValorMora : 0);
    }
}
