using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Entidades.Alumnos;
using SRAUMOAR.Entidades.Generales;
using SRAUMOAR.Entidades.Materias;
using System.Collections.Generic;
using SRAUMOAR.Entidades.Docentes;
using SRAUMOAR.Entidades.Accesos;
using SRAUMOAR.Entidades.Procesos;
using SRAUMOAR.Entidades.Colecturia;
using SRAUMOAR.Entidades.Becas;

namespace SRAUMOAR.Modelos
{
    public class  Contexto : DbContext
    {
        public Contexto(DbContextOptions<Contexto> options) : base(options)
        {
        }

        public DbSet<Alumno> Alumno { get; set; } = null!;
        public DbSet<Departamento> Departamentos { get; set; } = null!;
        public DbSet<Distrito> Distritos { get; set; } = null!;
        public DbSet<Municipio> Municipios { get; set; } = null!;
        public DbSet<Facultad> Facultades { get; set; } = null!;
        public DbSet<Carrera> Carreras { get; set; } = null!;
        public DbSet<Profesion> Profesiones { get; set; } = null!;


        public DbSet<Materia> Materias { get; set; } 
        public DbSet<MateriaPrerequisito> MateriasPrerrequisitos { get; set; } 
        public DbSet<Pensum> Pensums { get; set; }

        public DbSet<Docente> Docentes { get; set; }
        public DbSet<NivelAcceso> NivelesAcceso { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }

        public DbSet<Ciclo> Ciclos { get; set; }
        public DbSet<Inscripcion> Inscripciones { get; set; }
        public DbSet<Arancel> Aranceles { get; set; }

        public DbSet<Grupo> Grupo { get; set; }
        public DbSet<MateriasGrupo> MateriasGrupo { get; set; }

        public DbSet<CobroArancel> CobrosArancel { get; set; }
        public DbSet<ActividadAcademica> ActividadesAcademicas { get; set; }

        public DbSet<DetallesCobroArancel> DetallesCobrosArancel { get; set; }

        public DbSet<MateriasInscritas> MateriasInscritas { get; set; }
        public DbSet<Notas> Notas { get; set; }
        public DbSet<EntidadBeca> InstitucionesBeca { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Arancel>()
           .Property(a => a.Costo)
           .HasPrecision(18, 2); // O .HasColumnType("decimal(18,2)")

            modelBuilder.Entity<Materia>()
                .HasMany(m => m.Prerrequisitos)
                .WithOne(mp => mp.Materia)
                .HasForeignKey(mp => mp.MateriaId)
                .OnDelete(DeleteBehavior.Restrict); // O la acción deseada en caso de eliminar una materia

            modelBuilder.Entity<MateriaPrerequisito>()
                .HasOne(mp => mp.PrerrequisoMateria)
                .WithMany()
                .HasForeignKey(mp => mp.PrerrequisoMateriaId)
                .OnDelete(DeleteBehavior.Restrict); // O la acción deseada en caso de eliminar una materia
        }
        public DbSet<SRAUMOAR.Entidades.Accesos.NivelAcceso> NivelAcceso { get; set; } = default!;



    }
}
