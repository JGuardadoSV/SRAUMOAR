using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    //Nombre de la tabla Pago

    [Table("Pagos")]
    public class Pago
    {
        public int PagoId { get; set; }
        //Formato de fecha DD/MM/YYYY y displayname Fecha, son data annotation
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; }
        public decimal Monto { get; set; }
        public decimal Entrego { get; set; }
        public decimal Cambio { get; set; }
        public bool Procesado { get; set; }
        //FK con la entidad Matricula
        [ForeignKey("MatriculaId")]
        public int MatriculaId { get; set; }
        public virtual Matricula? Matricula { get; set; }


        //FK con la entidad Inscripcion
        [ForeignKey("InscripcionId")]
        public int InscripcionId { get; set; }
        public virtual Inscripcion? Inscripcion { get; set; }
        

    }

}
