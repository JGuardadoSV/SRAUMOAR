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
                new ConfiguracionReporte { Id = 11, Reporte = "CertificacionNotas", Clave = "RectoraCertificacion", Valor = "La infrascrita, Rectora de la Universidad Monseñor Oscar Arnulfo Romero, certifica que la firma que aparece al pie de la certificación global de notas es auténtica y es la misma que usa el {SecretarioNombre}, {SecretarioCargo} de esta universidad.", Descripcion = "Texto de certificación de la Rectora" },
                new ConfiguracionReporte { Id = 12, Reporte = "ConstanciaCursoPreUniversitario", Clave = "TituloArea", Valor = "RECTORIA", Descripcion = "Título del área de la constancia" },
                new ConfiguracionReporte { Id = 13, Reporte = "ConstanciaCursoPreUniversitario", Clave = "TituloReporte", Valor = "CONSTANCIA DE CURSO PRE UNIVERSITARIO", Descripcion = "Nombre del reporte" },
                new ConfiguracionReporte { Id = 14, Reporte = "ConstanciaCursoPreUniversitario", Clave = "Cuerpo", Valor = "El Infrascrito Rector de la Universidad Monseñor Oscar Arnulfo Romero, por la presente hace constar que: {nombrealumno}, realizó y aprobó su curso pre-universitario, impartido en esta institución desde el día {fechaInicioCurso} al {fechaFinCurso}.", Descripcion = "Cuerpo principal de la constancia" },
                new ConfiguracionReporte { Id = 15, Reporte = "ConstanciaCursoPreUniversitario", Clave = "Cierre", Valor = "Y para ser presentada a la Administración de Registro Académico, se extiende la presente Constancia, en {lugarExpedicion} {fechaExpedicion}.", Descripcion = "Párrafo de cierre de la constancia" },
                new ConfiguracionReporte { Id = 16, Reporte = "ConstanciaCursoPreUniversitario", Clave = "FirmaNombre", Valor = "", Descripcion = "Nombre del firmante de la constancia" },
                new ConfiguracionReporte { Id = 17, Reporte = "ConstanciaCursoPreUniversitario", Clave = "FirmaCargo", Valor = "RECTOR", Descripcion = "Cargo del firmante de la constancia" },
                new ConfiguracionReporte { Id = 18, Reporte = "ConstanciaCursoPreUniversitario", Clave = "LugarExpedicion", Valor = "Tejutla, Chalatenango", Descripcion = "Lugar de expedición de la constancia" },
                new ConfiguracionReporte { Id = 19, Reporte = "ConstanciaHorario", Clave = "TituloReporte", Valor = "CONSTANCIA DE HORARIO", Descripcion = "Nombre del reporte de horario" },
                new ConfiguracionReporte { Id = 20, Reporte = "ConstanciaHorario", Clave = "EmisorCargo", Valor = "Decano de la Facultad de Ciencias y Humanidades y Administrador en Funciones Ad Honorem de Registro Académico", Descripcion = "Cargo de la persona que emite la constancia" },
                new ConfiguracionReporte { Id = 21, Reporte = "ConstanciaHorario", Clave = "LugarInstitucion", Valor = "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango", Descripcion = "Ubicación institucional para el cuerpo de la constancia" },
                new ConfiguracionReporte { Id = 22, Reporte = "ConstanciaHorario", Clave = "Destinatario", Valor = "PROCURADURÍA GENERAL DE LA REPÚBLICA DE EL SALVADOR", Descripcion = "Institución o destino donde se presentará la constancia" },
                new ConfiguracionReporte { Id = 23, Reporte = "ConstanciaHorario", Clave = "Cuerpo", Valor = "El Infrascrito {emisorCargo}, de la Universidad Monseñor Oscar Arnulfo Romero, en el {lugarInstitucion}, HACE CONSTAR QUE: {nombrealumno}, con carnet N° {carnet}, ES {alumnoalumna} {activoactiva} DEL {cicloRomano} CICLO, de la {facultad}, en la Carrera de {carrera}, del Ciclo Académico {cicloAcademico}, el cual dio inicio el {fechaInicioCiclo} y finalizará el día {fechaFinCiclo}, habiendo inscrito {cantidadMateriasLetras} materias en el horario siguiente:", Descripcion = "Cuerpo principal de la constancia de horario" },
                new ConfiguracionReporte { Id = 24, Reporte = "ConstanciaHorario", Clave = "Cierre", Valor = "Y para ser presentada a la {destinatario}, se le extiende la presente en el {lugarExpedicion}, {fechaExpedicion}.", Descripcion = "Párrafo de cierre de la constancia de horario" },
                new ConfiguracionReporte { Id = 25, Reporte = "ConstanciaHorario", Clave = "LugarExpedicion", Valor = "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango", Descripcion = "Lugar de expedición de la constancia de horario" },
                new ConfiguracionReporte { Id = 26, Reporte = "ConstanciaHorario", Clave = "FirmaNombre", Valor = "LIC. JOSÉ AUGUSTO HERNÁNDEZ GONZÁLEZ", Descripcion = "Nombre del firmante de la constancia de horario" },
                new ConfiguracionReporte { Id = 27, Reporte = "ConstanciaHorario", Clave = "FirmaCargoLinea1", Valor = "DECANO DE LA FACULTAD DE CIENCIAS Y HUMANIDADES Y", Descripcion = "Primera línea del cargo del firmante" },
                new ConfiguracionReporte { Id = 28, Reporte = "ConstanciaHorario", Clave = "FirmaCargoLinea2", Valor = "ADMINISTRADOR EN FUNCIONES AD HONOREM", Descripcion = "Segunda línea del cargo del firmante" }
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
