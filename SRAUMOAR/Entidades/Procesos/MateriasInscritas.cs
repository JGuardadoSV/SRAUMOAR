using SRAUMOAR.Entidades.Alumnos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SRAUMOAR.Entidades.Procesos
{
    public class MateriasInscritas
    {
        public int MateriasInscritasId { get; set; }

        //fecha con valor por defecto
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
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

        // Nota de recuperación específica para esta materia inscrita
        [Display(Name = "Nota de Recuperación")]
        [Range(0, 10, ErrorMessage = "La nota de recuperación debe estar entre 0 y 10")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal? NotaRecuperacion { get; set; }

        [Display(Name = "Fecha de Recuperación")]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        public DateTime? FechaRecuperacion { get; set; }

        //propiedad de navegacion con notas
        public virtual ICollection<Notas>? Notas { get; set; }



        // Método para obtener las notas o 0 si no existen
        public decimal ObtenerNota(int tipo)
        {
            var nota = Notas?.FirstOrDefault(n => n.ActividadAcademica.TipoActividad == tipo)?.Nota;
            return  0;
        }

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

        //fecha de ingreso automaticamente NOW
        public DateTime FechaRegistro { get; set; } = DateTime.Now;


    }


}
