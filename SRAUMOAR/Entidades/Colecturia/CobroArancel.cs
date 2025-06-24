using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Procesos;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Colecturia
{
    public class CobroArancel
    {
        public int CobroArancelId { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        [Display(Name = "Fecha")] 
        public DateTime Fecha { get; set; } = DateTime.Now;

        //[Display(Name = "Arancel")]
        //public int? ArancelId { get; set; }
        //public virtual Arancel? Arancel { get; set; }

        [Display(Name = "Alumno")]
        public int? AlumnoId { get; set; }
        public virtual Alumno? Alumno { get; set; }

        [Display(Name = "Ciclo")]
        public int? CicloId { get; set; }
        public virtual Ciclo? Ciclo { get; set; }

        public decimal EfectivoRecibido { get; set; }
        public decimal Total { get; set; }
        public decimal Cambio { get; set; }
        public string? nota { get; set; } = "";
        public string? CodigoGeneracion { get; set; }

        public virtual ICollection<DetallesCobroArancel>? DetallesCobroArancel { get; set; }
    }

    public class DetallesCobroArancel
    {
        public int DetallesCobroArancelId { get; set; }

        //llave foranea
        public int CobroArancelId { get; set; }
        public virtual CobroArancel? CobroArancel { get; set; }

        public int ArancelId { get; set; }
        public virtual Arancel? Arancel { get; set; }

        public decimal costo { get; set; }
    }
}
