using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class municipioidalumno_codfacultad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoFacultad",
                table: "Facultades",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MunicipioId",
                table: "Alumno",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alumno_MunicipioId",
                table: "Alumno",
                column: "MunicipioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Alumno_Municipios_MunicipioId",
                table: "Alumno",
                column: "MunicipioId",
                principalTable: "Municipios",
                principalColumn: "MunicipioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alumno_Municipios_MunicipioId",
                table: "Alumno");

            migrationBuilder.DropIndex(
                name: "IX_Alumno_MunicipioId",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "CodigoFacultad",
                table: "Facultades");

            migrationBuilder.DropColumn(
                name: "MunicipioId",
                table: "Alumno");
        }
    }
}
