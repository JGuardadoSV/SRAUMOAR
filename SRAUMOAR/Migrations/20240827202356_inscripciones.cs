using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class inscripciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inscripciones",
                columns: table => new
                {
                    InscripcionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    AlumnoId = table.Column<int>(type: "int", nullable: false),
                    CicloId = table.Column<int>(type: "int", nullable: false),
                    CarreraId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inscripciones", x => x.InscripcionId);
                    table.ForeignKey(
                        name: "FK_Inscripciones_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "AlumnoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inscripciones_Carreras_CarreraId",
                        column: x => x.CarreraId,
                        principalTable: "Carreras",
                        principalColumn: "CarreraId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inscripciones_Ciclos_CicloId",
                        column: x => x.CicloId,
                        principalTable: "Ciclos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_AlumnoId",
                table: "Inscripciones",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_CarreraId",
                table: "Inscripciones",
                column: "CarreraId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_CicloId",
                table: "Inscripciones",
                column: "CicloId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inscripciones");
        }
    }
}
