using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Generales
{
    public enum TipoDocumento
    {
        [Display(Name = "NIT")]
        NIT = 36,

        [Display(Name = "DUI")]
        DUI = 13,

        [Display(Name = "Carnet de Residente")]
        CarnetResidente = 2,

        [Display(Name = "Pasaporte")]
        Pasaporte = 3,

        [Display(Name = "Otro")]
        Otro = 37
    }
    [Table("donantes")]
    public class Donantes
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Tipo de Documento")]
        [Column("tipoDocumento")]
        public TipoDocumento TipoDocumento { get; set; }

        [Display(Name = "Número de Documento")]
        [Column("numDocumento")]
        public string NumDocumento { get; set; }

        [Column("nrc")]
        public string Nrc { get; set; }

        [Display(Name = "Nombre del Donante")]
        [Column("nombre")]
        public string Nombre { get; set; }

        [Column("codActividad")]
        [Display(Name = "Código de Actividad")]
        public string CodActividad { get; set; }

        [Column("descActividad")]
        [Display(Name = "Descripción de la Actividad")]
        public string DescActividad { get; set; }

        [Column("direccion")]
        public string Direccion { get; set; }

        [Column("telefono")]
        public string Telefono { get; set; }

        [Column("correo")]
        public string Correo { get; set; }

        [Column("codDomiciliado")]
        [Display(Name = "Código Domiciliado")]
        public int CodDomiciliado { get; set; }

        [Column("codPais")]
        [Display(Name = "Código del País")]
        public string CodPais { get; set; }
    }
}
