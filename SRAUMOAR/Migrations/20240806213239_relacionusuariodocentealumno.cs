using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class relacionusuariodocentealumno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UsuarioId",
                table: "Docentes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsuarioId",
                table: "Alumno",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Docentes_UsuarioId",
                table: "Docentes",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Alumno_UsuarioId",
                table: "Alumno",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Alumno_Usuarios_UsuarioId",
                table: "Alumno",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario");

            migrationBuilder.AddForeignKey(
                name: "FK_Docentes_Usuarios_UsuarioId",
                table: "Docentes",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "IdUsuario");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alumno_Usuarios_UsuarioId",
                table: "Alumno");

            migrationBuilder.DropForeignKey(
                name: "FK_Docentes_Usuarios_UsuarioId",
                table: "Docentes");

            migrationBuilder.DropIndex(
                name: "IX_Docentes_UsuarioId",
                table: "Docentes");

            migrationBuilder.DropIndex(
                name: "IX_Alumno_UsuarioId",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Docentes");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Alumno");
        }
    }
}
