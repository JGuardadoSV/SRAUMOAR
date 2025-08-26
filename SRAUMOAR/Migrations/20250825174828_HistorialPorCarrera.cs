using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class HistorialPorCarrera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CarreraId",
                table: "HistorialAcademico",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialAcademico_CarreraId",
                table: "HistorialAcademico",
                column: "CarreraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HistorialAcademico_Carreras_CarreraId",
                table: "HistorialAcademico");

            migrationBuilder.DropIndex(
                name: "IX_HistorialAcademico_CarreraId",
                table: "HistorialAcademico");

            migrationBuilder.DropColumn(
                name: "CarreraId",
                table: "HistorialAcademico");
        }
    }
}
