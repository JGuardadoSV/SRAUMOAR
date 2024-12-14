using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class actividades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActividadesAcademicas",
                columns: table => new
                {
                    ActividadAcademicaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CicloId = table.Column<int>(type: "int", nullable: false),
                    ArancelId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActividadesAcademicas", x => x.ActividadAcademicaId);
                    table.ForeignKey(
                        name: "FK_ActividadesAcademicas_Ciclos_CicloId",
                        column: x => x.CicloId,
                        principalTable: "Ciclos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ActividadesAcademicas_aranceles_ArancelId",
                        column: x => x.ArancelId,
                        principalTable: "aranceles",
                        principalColumn: "ArancelId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesAcademicas_ArancelId",
                table: "ActividadesAcademicas",
                column: "ArancelId");

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesAcademicas_CicloId",
                table: "ActividadesAcademicas",
                column: "CicloId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActividadesAcademicas");
        }
    }
}
