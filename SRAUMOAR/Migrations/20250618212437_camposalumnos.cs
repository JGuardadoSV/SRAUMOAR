using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class camposalumnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PDUI",
                table: "Alumno",
                newName: "PSolicitudEquivalencia");

            migrationBuilder.AddColumn<bool>(
                name: "PExamenOrina",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PFotografias",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PHemograma",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PPaes",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PPreuniversitario",
                table: "Alumno",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PExamenOrina",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "PFotografias",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "PHemograma",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "PPaes",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "PPreuniversitario",
                table: "Alumno");

            migrationBuilder.RenameColumn(
                name: "PSolicitudEquivalencia",
                table: "Alumno",
                newName: "PDUI");
        }
    }
}
