using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.FacturacionElectronica;

[Table("ConfiguracionSmtp")]
public class ConfiguracionSmtp
{
    [Key] public int Id { get; set; }
    public bool Activo { get; set; } = true;
    [Required, StringLength(250)] public string Servidor { get; set; } = string.Empty;
    public int Puerto { get; set; } = 587;
    [StringLength(250)] public string? Usuario { get; set; }
    public string? PasswordCifrada { get; set; }
    [Required, StringLength(250)] public string EmailRemitente { get; set; } = string.Empty;
    [StringLength(250)] public string? NombreRemitente { get; set; }
    [StringLength(1000)] public string? CopiaOculta { get; set; }
    public bool UsarSsl { get; set; }
    public bool UsarTls { get; set; } = true;
    public int MaxReintentos { get; set; } = 5;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
