﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SRAUMOAR.Modelos;

#nullable disable

namespace SRAUMOAR.Migrations
{
    [DbContext(typeof(Contexto))]
    [Migration("20240828100124_agregar_uv")]
    partial class agregar_uv
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("SRAUMOAR.Entidades.Accesos.NivelAcceso", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Nombre")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("Id");

                    b.ToTable("NivelAcceso");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Accesos.Usuario", b =>
                {
                    b.Property<int>("IdUsuario")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("IdUsuario"));

                    b.Property<bool?>("Activo")
                        .HasColumnType("bit");

                    b.Property<string>("Clave")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<int>("NivelAccesoId")
                        .HasColumnType("int");

                    b.Property<string>("NombreUsuario")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("IdUsuario");

                    b.HasIndex("NivelAccesoId");

                    b.ToTable("Usuarios");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Alumnos.Alumno", b =>
                {
                    b.Property<int>("AlumnoId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("AlumnoId"));

                    b.Property<string>("Apellidos")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Carnet")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ContactoDeEmergencia")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("DireccionDeResidencia")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Estado")
                        .HasColumnType("int");

                    b.Property<DateTime>("FechaDeNacimiento")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("FechaDeRegistro")
                        .HasColumnType("datetime2");

                    b.Property<string>("Fotografia")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IngresoPorEquivalencias")
                        .HasColumnType("bit");

                    b.Property<string>("Nombres")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("NumeroDeEmergencia")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("TelefonoPrimario")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("TelefonoSecundario")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("UsuarioId")
                        .HasColumnType("int");

                    b.Property<string>("Whatsapp")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("AlumnoId");

                    b.HasIndex("UsuarioId");

                    b.ToTable("Alumno");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Docentes.Docente", b =>
                {
                    b.Property<int>("DocenteId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("DocenteId"));

                    b.Property<string>("Apellidos")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Direccion")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Dui")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("FechaDeNacimiento")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("FechaDeRegistro")
                        .HasColumnType("datetime2");

                    b.Property<string>("Nombres")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("ProfesionId")
                        .HasColumnType("int");

                    b.Property<int?>("UsuarioId")
                        .HasColumnType("int");

                    b.HasKey("DocenteId");

                    b.HasIndex("ProfesionId");

                    b.HasIndex("UsuarioId");

                    b.ToTable("Docentes");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Carrera", b =>
                {
                    b.Property<int>("CarreraId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("CarreraId"));

                    b.Property<bool>("Activa")
                        .HasColumnType("bit");

                    b.Property<int>("Ciclos")
                        .HasColumnType("int");

                    b.Property<string>("CodigoCarrera")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Duracion")
                        .HasColumnType("int");

                    b.Property<int>("FacultadId")
                        .HasColumnType("int");

                    b.Property<string>("NombreCarrera")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("CarreraId");

                    b.HasIndex("FacultadId");

                    b.ToTable("Carreras");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Departamento", b =>
                {
                    b.Property<int>("DepartamentoId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("DepartamentoId"));

                    b.Property<string>("NombreDepartamento")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("DepartamentoId");

                    b.ToTable("Departamentos");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Distrito", b =>
                {
                    b.Property<int>("DistritoId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("DistritoId"));

                    b.Property<int>("DepartamentoId")
                        .HasColumnType("int");

                    b.Property<string>("NombreDistrito")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("DistritoId");

                    b.HasIndex("DepartamentoId");

                    b.ToTable("Distritos");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Facultad", b =>
                {
                    b.Property<int>("FacultadId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("FacultadId"));

                    b.Property<bool>("Activa")
                        .HasColumnType("bit");

                    b.Property<string>("NombreFacultad")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("FacultadId");

                    b.ToTable("Facultades");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Municipio", b =>
                {
                    b.Property<int>("MunicipioId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("MunicipioId"));

                    b.Property<int>("DistritoId")
                        .HasColumnType("int");

                    b.Property<string>("NombreMunicipio")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("MunicipioId");

                    b.HasIndex("DistritoId");

                    b.ToTable("Municipios");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Profesion", b =>
                {
                    b.Property<int>("ProfesionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ProfesionId"));

                    b.Property<string>("NombreProfesion")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ProfesionId");

                    b.ToTable("Profesiones");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Materias.Materia", b =>
                {
                    b.Property<int>("MateriaId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("MateriaId"));

                    b.Property<int>("Ciclo")
                        .HasColumnType("int");

                    b.Property<string>("CodigoMateria")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Correlativo")
                        .HasColumnType("int");

                    b.Property<string>("NombreMateria")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("PensumId")
                        .HasColumnType("int");

                    b.Property<bool>("RequisitoBachillerato")
                        .HasColumnType("bit");

                    b.Property<int>("uv")
                        .HasColumnType("int");

                    b.HasKey("MateriaId");

                    b.HasIndex("PensumId");

                    b.ToTable("Materias");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Materias.MateriaPrerequisito", b =>
                {
                    b.Property<int>("MateriaPrerequisitoId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("MateriaPrerequisitoId"));

                    b.Property<int>("MateriaId")
                        .HasColumnType("int");

                    b.Property<int>("PrerrequisoMateriaId")
                        .HasColumnType("int");

                    b.HasKey("MateriaPrerequisitoId");

                    b.HasIndex("MateriaId");

                    b.HasIndex("PrerrequisoMateriaId");

                    b.ToTable("MateriaPrerequisitos");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Materias.Pensum", b =>
                {
                    b.Property<int>("PensumId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("PensumId"));

                    b.Property<bool>("Activo")
                        .HasColumnType("bit");

                    b.Property<int>("Anio")
                        .HasColumnType("int");

                    b.Property<int>("CarreraId")
                        .HasColumnType("int");

                    b.Property<string>("CodigoPensum")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("NombrePensum")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("PensumId");

                    b.HasIndex("CarreraId");

                    b.ToTable("Pensum");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Procesos.Ciclo", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<bool>("Activo")
                        .HasColumnType("bit");

                    b.Property<int>("CorrelativoCarnet")
                        .HasColumnType("int");

                    b.Property<DateTime>("FechaFin")
                        .HasColumnType("date");

                    b.Property<DateTime>("FechaInicio")
                        .HasColumnType("date");

                    b.Property<DateTime>("FechaRegistro")
                        .HasColumnType("datetime2");

                    b.Property<int>("NCiclo")
                        .HasColumnType("int");

                    b.Property<int>("anio")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Ciclos");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Procesos.Inscripcion", b =>
                {
                    b.Property<int>("InscripcionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("InscripcionId"));

                    b.Property<bool>("Activa")
                        .HasColumnType("bit");

                    b.Property<int>("AlumnoId")
                        .HasColumnType("int");

                    b.Property<int>("CarreraId")
                        .HasColumnType("int");

                    b.Property<int>("CicloId")
                        .HasColumnType("int");

                    b.Property<DateTime>("Fecha")
                        .HasColumnType("datetime2");

                    b.HasKey("InscripcionId");

                    b.HasIndex("AlumnoId");

                    b.HasIndex("CarreraId");

                    b.HasIndex("CicloId");

                    b.ToTable("Inscripciones");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Accesos.Usuario", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Accesos.NivelAcceso", "NivelAcceso")
                        .WithMany("Usuarios")
                        .HasForeignKey("NivelAccesoId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("NivelAcceso");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Alumnos.Alumno", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Accesos.Usuario", "Usuario")
                        .WithMany()
                        .HasForeignKey("UsuarioId");

                    b.Navigation("Usuario");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Docentes.Docente", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Generales.Profesion", "Profesion")
                        .WithMany()
                        .HasForeignKey("ProfesionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("SRAUMOAR.Entidades.Accesos.Usuario", "Usuario")
                        .WithMany()
                        .HasForeignKey("UsuarioId");

                    b.Navigation("Profesion");

                    b.Navigation("Usuario");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Carrera", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Generales.Facultad", "Facultad")
                        .WithMany("Carreras")
                        .HasForeignKey("FacultadId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Facultad");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Distrito", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Generales.Departamento", "Departamento")
                        .WithMany("Distritos")
                        .HasForeignKey("DepartamentoId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Departamento");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Municipio", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Generales.Distrito", "Distrito")
                        .WithMany("Municipios")
                        .HasForeignKey("DistritoId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Distrito");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Materias.Materia", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Materias.Pensum", "Pensum")
                        .WithMany("Materias")
                        .HasForeignKey("PensumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Pensum");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Materias.MateriaPrerequisito", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Materias.Materia", "Materia")
                        .WithMany("Prerrequisitos")
                        .HasForeignKey("MateriaId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("SRAUMOAR.Entidades.Materias.Materia", "PrerrequisoMateria")
                        .WithMany()
                        .HasForeignKey("PrerrequisoMateriaId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Materia");

                    b.Navigation("PrerrequisoMateria");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Materias.Pensum", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Generales.Carrera", "Carrera")
                        .WithMany("Pensums")
                        .HasForeignKey("CarreraId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Carrera");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Procesos.Inscripcion", b =>
                {
                    b.HasOne("SRAUMOAR.Entidades.Alumnos.Alumno", "Alumno")
                        .WithMany()
                        .HasForeignKey("AlumnoId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("SRAUMOAR.Entidades.Generales.Carrera", "Carrera")
                        .WithMany()
                        .HasForeignKey("CarreraId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("SRAUMOAR.Entidades.Procesos.Ciclo", "Ciclo")
                        .WithMany()
                        .HasForeignKey("CicloId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Alumno");

                    b.Navigation("Carrera");

                    b.Navigation("Ciclo");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Accesos.NivelAcceso", b =>
                {
                    b.Navigation("Usuarios");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Carrera", b =>
                {
                    b.Navigation("Pensums");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Departamento", b =>
                {
                    b.Navigation("Distritos");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Distrito", b =>
                {
                    b.Navigation("Municipios");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Generales.Facultad", b =>
                {
                    b.Navigation("Carreras");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Materias.Materia", b =>
                {
                    b.Navigation("Prerrequisitos");
                });

            modelBuilder.Entity("SRAUMOAR.Entidades.Materias.Pensum", b =>
                {
                    b.Navigation("Materias");
                });
#pragma warning restore 612, 618
        }
    }
}
