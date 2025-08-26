using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class HistorialAcademico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistorialAcademico",
                columns: table => new
                {
                    HistorialAcademicoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlumnoId = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialAcademico", x => x.HistorialAcademicoId);
                    table.ForeignKey(
                        name: "FK_HistorialAcademico_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "AlumnoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistorialCiclo",
                columns: table => new
                {
                    HistorialCicloId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistorialAcademicoId = table.Column<int>(type: "int", nullable: false),
                    CicloId = table.Column<int>(type: "int", nullable: false),
                    PensumId = table.Column<int>(type: "int", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialCiclo", x => x.HistorialCicloId);
                    table.ForeignKey(
                        name: "FK_HistorialCiclo_Ciclos_CicloId",
                        column: x => x.CicloId,
                        principalTable: "Ciclos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialCiclo_HistorialAcademico_HistorialAcademicoId",
                        column: x => x.HistorialAcademicoId,
                        principalTable: "HistorialAcademico",
                        principalColumn: "HistorialAcademicoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialCiclo_Pensum_PensumId",
                        column: x => x.PensumId,
                        principalTable: "Pensum",
                        principalColumn: "PensumId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialMateria",
                columns: table => new
                {
                    HistorialMateriaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistorialCicloId = table.Column<int>(type: "int", nullable: false),
                    MateriaId = table.Column<int>(type: "int", nullable: false),
                    Nota1 = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Nota2 = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Nota3 = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Nota4 = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Nota5 = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Nota6 = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Promedio = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Aprobada = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialMateria", x => x.HistorialMateriaId);
                    table.ForeignKey(
                        name: "FK_HistorialMateria_HistorialCiclo_HistorialCicloId",
                        column: x => x.HistorialCicloId,
                        principalTable: "HistorialCiclo",
                        principalColumn: "HistorialCicloId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistorialMateria_Materias_MateriaId",
                        column: x => x.MateriaId,
                        principalTable: "Materias",
                        principalColumn: "MateriaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialAcademico_AlumnoId",
                table: "HistorialAcademico",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialCiclo_CicloId",
                table: "HistorialCiclo",
                column: "CicloId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialCiclo_HistorialAcademicoId",
                table: "HistorialCiclo",
                column: "HistorialAcademicoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialCiclo_PensumId",
                table: "HistorialCiclo",
                column: "PensumId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialMateria_HistorialCicloId",
                table: "HistorialMateria",
                column: "HistorialCicloId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialMateria_MateriaId",
                table: "HistorialMateria",
                column: "MateriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialMateria");

            migrationBuilder.DropTable(
                name: "HistorialCiclo");

            migrationBuilder.DropTable(
                name: "HistorialAcademico");
        }
    }
}
