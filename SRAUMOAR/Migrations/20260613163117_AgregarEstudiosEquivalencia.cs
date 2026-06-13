using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEstudiosEquivalencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EstudiosEquivalencia",
                columns: table => new
                {
                    EstudioEquivalenciaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlumnoId = table.Column<int>(type: "int", nullable: false),
                    UniversidadOrigen = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CarreraOrigen = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaEstudio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaAprobacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstudiosEquivalencia", x => x.EstudioEquivalenciaId);
                    table.ForeignKey(
                        name: "FK_EstudiosEquivalencia_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "AlumnoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesEquivalencia",
                columns: table => new
                {
                    DetalleEquivalenciaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EstudioEquivalenciaId = table.Column<int>(type: "int", nullable: false),
                    MateriaOrigenCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MateriaOrigenNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NotaOrigen = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MateriaDestinoId = table.Column<int>(type: "int", nullable: false),
                    NotaEquivalencia = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesEquivalencia", x => x.DetalleEquivalenciaId);
                    table.ForeignKey(
                        name: "FK_DetallesEquivalencia_EstudiosEquivalencia_EstudioEquivalenciaId",
                        column: x => x.EstudioEquivalenciaId,
                        principalTable: "EstudiosEquivalencia",
                        principalColumn: "EstudioEquivalenciaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesEquivalencia_Materias_MateriaDestinoId",
                        column: x => x.MateriaDestinoId,
                        principalTable: "Materias",
                        principalColumn: "MateriaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesEquivalencia_EstudioEquivalenciaId",
                table: "DetallesEquivalencia",
                column: "EstudioEquivalenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesEquivalencia_MateriaDestinoId",
                table: "DetallesEquivalencia",
                column: "MateriaDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_EstudiosEquivalencia_AlumnoId",
                table: "EstudiosEquivalencia",
                column: "AlumnoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesEquivalencia");

            migrationBuilder.DropTable(
                name: "EstudiosEquivalencia");
        }
    }
}
