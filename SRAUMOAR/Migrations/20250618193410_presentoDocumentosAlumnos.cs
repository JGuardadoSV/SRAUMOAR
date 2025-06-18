using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class presentoDocumentosAlumnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PDUI",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PPartida",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PTitulo",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PDUI",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "PPartida",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "PTitulo",
                table: "Alumno");
        }
    }
}
