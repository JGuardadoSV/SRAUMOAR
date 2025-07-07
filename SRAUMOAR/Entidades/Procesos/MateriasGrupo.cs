using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Entidades.Materias;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace SRAUMOAR.Entidades.Procesos
{

    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? value.ToString();
        }
    }

    public enum DiaSemana
    {
        [Display(Name = "Lunes")]
        Lunes = 1,

        [Display(Name = "Martes")]
        Martes = 2,

        [Display(Name = "Miércoles")]
        Miercoles = 3,

        [Display(Name = "Jueves")]
        Jueves = 4,

        [Display(Name = "Viernes")]
        Viernes = 5,

        [Display(Name = "Sábado")]
        Sabado = 6,

        [Display(Name = "Domingo")]
        Domingo = 7,

        [Display(Name = "-")]
        NoAsignado = 0
    }

    [Table(name:"GruposMaterias")]
    public class MateriasGrupo
    {
        public int MateriasGrupoId { get; set; }

        public string? Aula { get; set; }
        
        [Required]
        [DataType(DataType.Time)]
        [Display(Name = "Hora de Inicio")] 
        public TimeSpan HoraInicio { get; set; }
        
        [Required]
        [DataType(DataType.Time)]
        [Display(Name = "Hora de Fin")] 
        public TimeSpan HoraFin { get; set; }

        [Required(ErrorMessage = "La Materia es requerida")]
        [Display(Name = "Materia")]
        public int MateriaId { get; set; } // Llave foránea
        public virtual Materia? Materia { get; set; } // Propiedad de navegación

        [Required(ErrorMessage = "El grupo es requerido")]
        [Display(Name = "Grupo")]
        
        public int GrupoId { get; set; } // Llave foránea
        public virtual Grupo? Grupo { get; set; } // Propiedad de navegación

        [Required(ErrorMessage = "El docente es requerido")]
        [Display(Name = "Docente encargado")]
        public int DocenteId { get; set; } // Llave foránea
        public virtual Docente? Docente { get; set; } // Propiedad de navegación

        [Required]
        [Display(Name = "Día de la Semana")]
        public DiaSemana Dia { get; set; }

        public string FormatearHora12Horas(TimeSpan hora) { DateTime dt = DateTime.Today.Add(hora); return dt.ToString("hh:mm tt"); }

        //propiedad de navegacion MateriasInscritas
        public virtual ICollection<MateriasInscritas>? MateriasInscritas { get; set; }

        public int TotalInscritos => MateriasInscritas?.Count ?? 0;

    }
}
