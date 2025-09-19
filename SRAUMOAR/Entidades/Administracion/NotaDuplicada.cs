using System.ComponentModel.DataAnnotations;

namespace SRAUMOAR.Entidades.Administracion
{
    public class NotaDuplicada
    {
        public int NotasId { get; set; }
        public int MateriasInscritasId { get; set; }
        public int ActividadAcademicaId { get; set; }
        public decimal Nota { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string NombreAlumno { get; set; } = string.Empty;
        public string NombreActividad { get; set; } = string.Empty;
        public int TipoActividad { get; set; }
        public int MateriaId { get; set; }
        public string NombreMateria { get; set; } = string.Empty;
        public string NombreGrupo { get; set; } = string.Empty;
        public int CantidadDuplicados { get; set; }
        public int EsMasReciente { get; set; }
        public string TipoActividadTexto => TipoActividad switch 
        { 
            1 => "Laboratorio", 
            2 => "Parcial", 
            _ => "Desconocido" 
        };
    }
}
