using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarArancelEspecializacionActividadAcademica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArancelEspecializacionId",
                table: "ActividadesAcademicas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesAcademicas_ArancelEspecializacionId",
                table: "ActividadesAcademicas",
                column: "ArancelEspecializacionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActividadesAcademicas_aranceles_ArancelEspecializacionId",
                table: "ActividadesAcademicas",
                column: "ArancelEspecializacionId",
                principalTable: "aranceles",
                principalColumn: "ArancelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActividadesAcademicas_aranceles_ArancelEspecializacionId",
                table: "ActividadesAcademicas");

            migrationBuilder.DropIndex(
                name: "IX_ActividadesAcademicas_ArancelEspecializacionId",
                table: "ActividadesAcademicas");

            migrationBuilder.DropColumn(
                name: "ArancelEspecializacionId",
                table: "ActividadesAcademicas");
        }
    }
}
