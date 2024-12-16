using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class notaseinscripciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Porcentaje",
                table: "ActividadesAcademicas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MateriasInscritas",
                columns: table => new
                {
                    MateriasInscritasId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaInscripcion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotaPromedio = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    AlumnoId = table.Column<int>(type: "int", nullable: false),
                    MateriasGrupoId = table.Column<int>(type: "int", nullable: false),
                    Aprobada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriasInscritas", x => x.MateriasInscritasId);
                    table.ForeignKey(
                        name: "FK_MateriasInscritas_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "AlumnoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MateriasInscritas_GruposMaterias_MateriasGrupoId",
                        column: x => x.MateriasGrupoId,
                        principalTable: "GruposMaterias",
                        principalColumn: "MateriasGrupoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notas",
                columns: table => new
                {
                    NotasId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nota = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MateriasInscritasId = table.Column<int>(type: "int", nullable: false),
                    ActividadAcademicaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notas", x => x.NotasId);
                    table.ForeignKey(
                        name: "FK_Notas_ActividadesAcademicas_ActividadAcademicaId",
                        column: x => x.ActividadAcademicaId,
                        principalTable: "ActividadesAcademicas",
                        principalColumn: "ActividadAcademicaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notas_MateriasInscritas_MateriasInscritasId",
                        column: x => x.MateriasInscritasId,
                        principalTable: "MateriasInscritas",
                        principalColumn: "MateriasInscritasId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MateriasInscritas_AlumnoId",
                table: "MateriasInscritas",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_MateriasInscritas_MateriasGrupoId",
                table: "MateriasInscritas",
                column: "MateriasGrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_Notas_ActividadAcademicaId",
                table: "Notas",
                column: "ActividadAcademicaId");

            migrationBuilder.CreateIndex(
                name: "IX_Notas_MateriasInscritasId",
                table: "Notas",
                column: "MateriasInscritasId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notas");

            migrationBuilder.DropTable(
                name: "MateriasInscritas");

            migrationBuilder.DropColumn(
                name: "Porcentaje",
                table: "ActividadesAcademicas");
        }
    }
}
