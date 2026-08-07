using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.FacturacionElectronica;

[Table("CatalogosFe")]
public class CatalogoFeItem
{
    [Key] public long Id { get; set; }
    [Required, StringLength(10)] public string CodigoCatalogo { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Version { get; set; } = "1.1";
    [Required, StringLength(50)] public string Codigo { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Descripcion { get; set; } = string.Empty;
    [StringLength(500)] public string? ValorAuxiliar { get; set; }
    public bool Activo { get; set; } = true;
    public int Orden { get; set; }
}
