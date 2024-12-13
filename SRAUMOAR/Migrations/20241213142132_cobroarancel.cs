using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class cobroarancel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CobrosArancel",
                columns: table => new
                {
                    CobroArancelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArancelId = table.Column<int>(type: "int", nullable: true),
                    AlumnoId = table.Column<int>(type: "int", nullable: true),
                    CicloId = table.Column<int>(type: "int", nullable: true),
                    EfectivoRecibido = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cambio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    nota = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CobrosArancel", x => x.CobroArancelId);
                    table.ForeignKey(
                        name: "FK_CobrosArancel_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "AlumnoId");
                    table.ForeignKey(
                        name: "FK_CobrosArancel_Ciclos_CicloId",
                        column: x => x.CicloId,
                        principalTable: "Ciclos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CobrosArancel_aranceles_ArancelId",
                        column: x => x.ArancelId,
                        principalTable: "aranceles",
                        principalColumn: "ArancelId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CobrosArancel_AlumnoId",
                table: "CobrosArancel",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_CobrosArancel_ArancelId",
                table: "CobrosArancel",
                column: "ArancelId");

            migrationBuilder.CreateIndex(
                name: "IX_CobrosArancel_CicloId",
                table: "CobrosArancel",
                column: "CicloId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CobrosArancel");
        }
    }
}
