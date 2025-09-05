using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Modelos
{
    public class MateriaHistorialModel
    {
        public int MateriaId { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        public decimal Nota1 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        public decimal Nota2 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        public decimal Nota3 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        public decimal Nota4 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        public decimal Nota5 { get; set; }
        
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        public decimal Nota6 { get; set; }
        
        [Range(0, 10, ErrorMessage = "El promedio debe estar entre 0 y 10")]
        public decimal Promedio { get; set; }
        
        public bool Aprobada { get; set; }
        
        public bool Equivalencia { get; set; }
    }
}
