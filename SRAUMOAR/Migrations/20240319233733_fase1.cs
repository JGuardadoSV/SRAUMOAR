using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class fase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Alumno",
                columns: table => new
                {
                    AlumnoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombres = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaDeNacimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaDeRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TelefonoPrimario = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Whatsapp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TelefonoSecundario = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactoDeEmergencia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumeroDeEmergencia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DireccionDeResidencia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    IngresoPorEquivalencias = table.Column<bool>(type: "bit", nullable: false),
                    Fotografia = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alumno", x => x.AlumnoId);
                });

            migrationBuilder.CreateTable(
                name: "Departamentos",
                columns: table => new
                {
                    DepartamentoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreDepartamento = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departamentos", x => x.DepartamentoId);
                });

            migrationBuilder.CreateTable(
                name: "Facultades",
                columns: table => new
                {
                    FacultadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreFacultad = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facultades", x => x.FacultadId);
                });

            migrationBuilder.CreateTable(
                name: "Profesiones",
                columns: table => new
                {
                    ProfesionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreProfesion = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profesiones", x => x.ProfesionId);
                });

            migrationBuilder.CreateTable(
                name: "Distritos",
                columns: table => new
                {
                    DistritoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreDistrito = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartamentoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distritos", x => x.DistritoId);
                    table.ForeignKey(
                        name: "FK_Distritos_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "DepartamentoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Carreras",
                columns: table => new
                {
                    CarreraId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreCarrera = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigoCarrera = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duracion = table.Column<int>(type: "int", nullable: false),
                    Ciclos = table.Column<int>(type: "int", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FacultadId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carreras", x => x.CarreraId);
                    table.ForeignKey(
                        name: "FK_Carreras_Facultades_FacultadId",
                        column: x => x.FacultadId,
                        principalTable: "Facultades",
                        principalColumn: "FacultadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Municipios",
                columns: table => new
                {
                    MunicipioId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreMunicipio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DistritoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Municipios", x => x.MunicipioId);
                    table.ForeignKey(
                        name: "FK_Municipios_Distritos_DistritoId",
                        column: x => x.DistritoId,
                        principalTable: "Distritos",
                        principalColumn: "DistritoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pensum",
                columns: table => new
                {
                    PensumId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombrePensum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigoPensum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Anio = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CarreraId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pensum", x => x.PensumId);
                    table.ForeignKey(
                        name: "FK_Pensum_Carreras_CarreraId",
                        column: x => x.CarreraId,
                        principalTable: "Carreras",
                        principalColumn: "CarreraId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Materias",
                columns: table => new
                {
                    MateriaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreMateria = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigoMateria = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PensumId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materias", x => x.MateriaId);
                    table.ForeignKey(
                        name: "FK_Materias_Pensum_PensumId",
                        column: x => x.PensumId,
                        principalTable: "Pensum",
                        principalColumn: "PensumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MateriaPrerequisitos",
                columns: table => new
                {
                    MateriaPrerequisitoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MateriaId = table.Column<int>(type: "int", nullable: false),
                    PrerrequisoMateriaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriaPrerequisitos", x => x.MateriaPrerequisitoId);
                    table.ForeignKey(
                        name: "FK_MateriaPrerequisitos_Materias_MateriaId",
                        column: x => x.MateriaId,
                        principalTable: "Materias",
                        principalColumn: "MateriaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MateriaPrerequisitos_Materias_PrerrequisoMateriaId",
                        column: x => x.PrerrequisoMateriaId,
                        principalTable: "Materias",
                        principalColumn: "MateriaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Carreras_FacultadId",
                table: "Carreras",
                column: "FacultadId");

            migrationBuilder.CreateIndex(
                name: "IX_Distritos_DepartamentoId",
                table: "Distritos",
                column: "DepartamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_MateriaPrerequisitos_MateriaId",
                table: "MateriaPrerequisitos",
                column: "MateriaId");

            migrationBuilder.CreateIndex(
                name: "IX_MateriaPrerequisitos_PrerrequisoMateriaId",
                table: "MateriaPrerequisitos",
                column: "PrerrequisoMateriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Materias_PensumId",
                table: "Materias",
                column: "PensumId");

            migrationBuilder.CreateIndex(
                name: "IX_Municipios_DistritoId",
                table: "Municipios",
                column: "DistritoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pensum_CarreraId",
                table: "Pensum",
                column: "CarreraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alumno");

            migrationBuilder.DropTable(
                name: "MateriaPrerequisitos");

            migrationBuilder.DropTable(
                name: "Municipios");

            migrationBuilder.DropTable(
                name: "Profesiones");

            migrationBuilder.DropTable(
                name: "Materias");

            migrationBuilder.DropTable(
                name: "Distritos");

            migrationBuilder.DropTable(
                name: "Pensum");

            migrationBuilder.DropTable(
                name: "Departamentos");

            migrationBuilder.DropTable(
                name: "Carreras");

            migrationBuilder.DropTable(
                name: "Facultades");
        }
    }
}
