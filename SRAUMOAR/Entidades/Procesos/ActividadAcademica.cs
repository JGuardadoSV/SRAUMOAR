﻿using SRAUMOAR.Entidades.Procesos;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Procesos
{
    [Table("ActividadesAcademicas")]
    public class ActividadAcademica
    {
        [Key]
        public int ActividadAcademicaId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Registro")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required]
        [StringLength(100)]
        public string? Nombre { get; set; }

        [Required]
        [DisplayFormat(ApplyFormatInEditMode = false, DataFormatString = "{0:dd/MM/yyyy}")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Inicio")]
        public DateTime FechaInicio { get; set; }

        [Required]
        [DisplayFormat(ApplyFormatInEditMode = false, DataFormatString = "{0:dd/MM/yyyy}")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Finalización")]
        public DateTime FechaFin { get; set; }

        [ForeignKey("Ciclo")]
        public int CicloId { get; set; }

        [ForeignKey("Arancel")]
        [Display(Name = "Arancel requerido")]
        public int ArancelId { get; set; }

        // Relaciones de navegación
        public virtual Ciclo? Ciclo { get; set; }
        public virtual Arancel? Arancel { get; set; }
    }
}


