using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Procesos;
using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Colecturia
{
    public class CobroArancel
    {

        public int CobroArancelId { get; set; }

        [Display(Name = "Fecha")] 
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Display(Name = "Arancel")]
        public int? ArancelId { get; set; }
        public virtual Arancel? Arancel { get; set; }

        [Display(Name = "Alumno")]
        public int? AlumnoId { get; set; }
        public virtual Alumno? Alumno { get; set; }

        [Display(Name = "Ciclo")]
        public int? CicloId { get; set; }
        public virtual Ciclo? Ciclo { get; set; }

        public decimal EfectivoRecibido { get; set; }
        public decimal Cambio { get; set; }
        public string nota { get; set; } = "";



    }
}
