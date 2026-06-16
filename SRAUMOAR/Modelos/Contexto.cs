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
        public DbSet<CausaDesercion> CausasDesercion { get; set; }
        public DbSet<DesercionAlumno> DesercionesAlumno { get; set; }
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
        public DbSet<ConfiguracionReporte> ConfiguracionesReportes { get; set; } = null!;
        public DbSet<EstudioEquivalencia> EstudiosEquivalencia { get; set; } = null!;
        public DbSet<DetalleEquivalencia> DetallesEquivalencia { get; set; } = null!;
        public DbSet<DetalleEquivalenciaOrigen> DetallesEquivalenciaOrigen { get; set; } = null!;
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

            modelBuilder.Entity<DesercionAlumno>()
                .HasOne(d => d.Alumno)
                .WithMany()
                .HasForeignKey(d => d.AlumnoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DesercionAlumno>()
                .HasOne(d => d.Ciclo)
                .WithMany()
                .HasForeignKey(d => d.CicloId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DesercionAlumno>()
                .HasOne(d => d.CausaDesercion)
                .WithMany()
                .HasForeignKey(d => d.CausaDesercionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DesercionAlumno>()
                .HasIndex(d => new { d.AlumnoId, d.CicloId })
                .IsUnique();

            modelBuilder.Entity<CausaDesercion>().HasData(
                new CausaDesercion { CausaDesercionId = 1, Nombre = "Abandono", Activo = true },
                new CausaDesercion { CausaDesercionId = 2, Nombre = "Deserción", Activo = true },
                new CausaDesercion { CausaDesercionId = 3, Nombre = "Retiro de ciclo", Activo = true }
            );

            modelBuilder.Entity<ConfiguracionReporte>().HasData(
                new ConfiguracionReporte { Id = 1, Reporte = "CertificacionNotas", Clave = "DireccionLinea1", Valor = "", Descripcion = "Primera línea de dirección de la universidad" },
                new ConfiguracionReporte { Id = 2, Reporte = "CertificacionNotas", Clave = "DireccionLinea2", Valor = "", Descripcion = "Segunda línea de dirección de la universidad" },
                new ConfiguracionReporte { Id = 3, Reporte = "CertificacionNotas", Clave = "DireccionLinea3", Valor = "", Descripcion = "Tercera línea de dirección de la universidad" },
                new ConfiguracionReporte { Id = 4, Reporte = "CertificacionNotas", Clave = "Introduccion", Valor = "", Descripcion = "Párrafo de texto introductorio en negrita" },
                new ConfiguracionReporte { Id = 5, Reporte = "CertificacionNotas", Clave = "FirmaNombre", Valor = "", Descripcion = "Nombre del firmante en el pie de página" },
                new ConfiguracionReporte { Id = 6, Reporte = "CertificacionNotas", Clave = "FirmaCargo", Valor = "", Descripcion = "Cargo del firmante en el pie de página" },
                new ConfiguracionReporte { Id = 7, Reporte = "CertificacionNotas", Clave = "FirmaSublinea", Valor = "", Descripcion = "Información adicional o segunda línea de firma (ej. Sello o Registro)" },
                new ConfiguracionReporte { Id = 8, Reporte = "CertificacionNotas", Clave = "RectoraNombre", Valor = "LICDA. CARMEN NAVAS ESCOBAR DE MEJÍA", Descripcion = "Nombre de la Rectora" },
                new ConfiguracionReporte { Id = 9, Reporte = "CertificacionNotas", Clave = "RectoraCargo", Valor = "RECTORA", Descripcion = "Cargo de la Rectora" },
                new ConfiguracionReporte { Id = 10, Reporte = "CertificacionNotas", Clave = "ConfrontadoPor", Valor = "LIC. JOSE AUGUSTO HERNANDEZ GONZALEZ", Descripcion = "Nombre de la persona que confronta" },
                new ConfiguracionReporte { Id = 11, Reporte = "CertificacionNotas", Clave = "RectoraCertificacion", Valor = "La infrascrita, Rectora de la Universidad Monseñor Oscar Arnulfo Romero, certifica que la firma que aparece al pie de la certificación global de notas es auténtica y es la misma que usa el {SecretarioNombre}, {SecretarioCargo} de esta universidad.", Descripcion = "Texto de certificación de la Rectora" }
            );

            // Relaciones para Estudios de Equivalencia
            modelBuilder.Entity<DetalleEquivalencia>()
                .HasOne(d => d.EstudioEquivalencia)
                .WithMany(e => e.Detalles)
                .HasForeignKey(d => d.EstudioEquivalenciaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DetalleEquivalencia>()
                .HasOne(d => d.MateriaDestino)
                .WithMany()
                .HasForeignKey(d => d.MateriaDestinoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EstudioEquivalencia>()
                .HasOne(e => e.Alumno)
                .WithMany()
                .HasForeignKey(e => e.AlumnoId)
                .OnDelete(DeleteBehavior.Restrict);

        }
        public DbSet<SRAUMOAR.Entidades.Accesos.NivelAcceso> NivelAcceso { get; set; } = default!;



    }
}
