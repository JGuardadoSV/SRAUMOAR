using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class datosextrasestudiante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Casado",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DepartamentoNacimiento",
                table: "Alumno",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EstudiosFinanciadoPor",
                table: "Alumno",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MunicipioNacimiento",
                table: "Alumno",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Casado",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "DepartamentoNacimiento",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "EstudiosFinanciadoPor",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "MunicipioNacimiento",
                table: "Alumno");
        }
    }
}
