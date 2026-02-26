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
using SRAUMOAR.Entidades.Historial;
using SRAUMOAR.Entidades;

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
        public DbSet<CodigoActividadEconomica> CodigosActividadEconomica { get; set; } = null!;


        public DbSet<Materia> Materias { get; set; } 
        public DbSet<MateriaPrerequisito> MateriasPrerrequisitos { get; set; } 
        public DbSet<Pensum> Pensums { get; set; }

        public DbSet<Docente> Docentes { get; set; }
        public DbSet<NivelAcceso> NivelesAcceso { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<ModuloPermiso> ModulosPermiso { get; set; }
        public DbSet<PermisoModuloRol> PermisosModuloRol { get; set; }

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

        public DbSet<Becados> Becados { get; set; }
        public DbSet<ArancelBecado> ArancelesBecados { get; set; }
        public DbSet<registroDTE> registroDTEs { get; set; }
        public DbSet<Donantes> Donantes { get; set; }
        public DbSet<DteCorrelativo> DteCorrelativos { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        
        // DbSets para Historial Académico
        public DbSet<HistorialAcademico> HistorialAcademico { get; set; } = null!;
        public DbSet<HistorialCiclo> HistorialCiclo { get; set; } = null!;
        public DbSet<HistorialMateria> HistorialMateria { get; set; } = null!;
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

            // Configuración para Historial Académico
            modelBuilder.Entity<HistorialAcademico>()
                .HasOne(ha => ha.Alumno)
                .WithMany()
                .HasForeignKey(ha => ha.AlumnoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HistorialAcademico>()
                .HasOne(ha => ha.Carrera)
                .WithMany()
                .HasForeignKey(ha => ha.CarreraId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<HistorialCiclo>()
                .HasOne(hc => hc.HistorialAcademico)
                .WithMany(ha => ha.CiclosHistorial)
                .HasForeignKey(hc => hc.HistorialAcademicoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HistorialMateria>()
                .HasOne(hm => hm.HistorialCiclo)
                .WithMany(hc => hc.MateriasHistorial)
                .HasForeignKey(hm => hm.HistorialCicloId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HistorialMateria>()
                .HasOne(hm => hm.Materia)
                .WithMany()
                .HasForeignKey(hm => hm.MateriaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<HistorialCiclo>()
                .HasOne(hc => hc.Pensum)
                .WithMany()
                .HasForeignKey(hc => hc.PensumId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ModuloPermiso>()
                .HasIndex(m => m.Codigo)
                .IsUnique();

            modelBuilder.Entity<PermisoModuloRol>()
                .HasIndex(p => new { p.ModuloPermisoId, p.NivelAccesoId })
                .IsUnique();

            modelBuilder.Entity<PermisoModuloRol>()
                .HasOne(p => p.ModuloPermiso)
                .WithMany(m => m.PermisosPorRol)
                .HasForeignKey(p => p.ModuloPermisoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PermisoModuloRol>()
                .HasOne(p => p.NivelAcceso)
                .WithMany()
                .HasForeignKey(p => p.NivelAccesoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
        public DbSet<SRAUMOAR.Entidades.Accesos.NivelAcceso> NivelAcceso { get; set; } = default!;



    }
}
