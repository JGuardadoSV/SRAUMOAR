using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class becados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlumnosBecados",
                columns: table => new
                {
                    BecadosId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false),
                    TipoBeca = table.Column<int>(type: "int", nullable: false),
                    AlumnoId = table.Column<int>(type: "int", nullable: false),
                    EntidadBecaId = table.Column<int>(type: "int", nullable: false),
                    CicloId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlumnosBecados", x => x.BecadosId);
                    table.ForeignKey(
                        name: "FK_AlumnosBecados_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "AlumnoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlumnosBecados_Ciclos_CicloId",
                        column: x => x.CicloId,
                        principalTable: "Ciclos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlumnosBecados_InstitucionesBeca_EntidadBecaId",
                        column: x => x.EntidadBecaId,
                        principalTable: "InstitucionesBeca",
                        principalColumn: "EntidadBecaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlumnosBecados_AlumnoId",
                table: "AlumnosBecados",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_AlumnosBecados_CicloId",
                table: "AlumnosBecados",
                column: "CicloId");

            migrationBuilder.CreateIndex(
                name: "IX_AlumnosBecados_EntidadBecaId",
                table: "AlumnosBecados",
                column: "EntidadBecaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlumnosBecados");
        }
    }
}
