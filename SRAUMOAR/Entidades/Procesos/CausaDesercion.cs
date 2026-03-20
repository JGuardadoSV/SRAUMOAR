using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Procesos
{
    public class CausaDesercion
    {
        [Key]
        public int CausaDesercionId { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;
    }
}
