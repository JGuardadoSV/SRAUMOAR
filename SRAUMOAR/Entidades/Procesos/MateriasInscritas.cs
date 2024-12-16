using SRAUMOAR.Entidades.Alumnos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    public class MateriasInscritas
    {
        public int MateriasInscritasId { get; set; }

        //fecha con valor por defecto
        public DateTime FechaInscripcion { get; set; }= DateTime.Now;



        [Display(Name = "Promedio")]
        [Range(0, 10, ErrorMessage = "El promedio debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal NotaPromedio { get; set; } = 0;

        //Relaciones Con Alumnos y con GruposMaterias
        [Required(ErrorMessage = "El Alumno es requerido")]
        [Display(Name = "Alumno")]
        public int AlumnoId { get; set; } // Llave foránea
        public virtual Alumno? Alumno { get; set; } // Propiedad de navegación

        [Required(ErrorMessage = "La materia es requerida")]
        [Display(Name = "Materia")]
        public int MateriasGrupoId { get; set; } // Llave foránea
        public virtual MateriasGrupo? MateriasGrupo { get; set; } // Propiedad de navegación

        //columna para guardar si esta aprovada
        public bool Aprobada { get; set; } = false;

        //propiedad de navegacion con notas
        public virtual ICollection<Notas>? Notas { get; set; }

    }

   //clase para guardar notas
   public class Notas
    {
        public int NotasId { get; set; }
        
        [Required(ErrorMessage = "La nota es requerida")]
        [Display(Name = "Nota")]
        [Range(0, 10, ErrorMessage = "La nota debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Nota { get; set; }

        //relaciones 
        public int MateriasInscritasId { get; set; } // Llave foránea
        public virtual MateriasInscritas? MateriasInscritas { get; set; }

        public int ActividadAcademicaId { get; set; }
        public virtual ActividadAcademica? ActividadAcademica { get; set; }
    }


}
