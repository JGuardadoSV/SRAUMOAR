using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class carreraalumno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CarreraId",
                table: "Alumno",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alumno_CarreraId",
                table: "Alumno",
                column: "CarreraId");

            migrationBuilder.AddForeignKey(
                name: "FK_Alumno_Carreras_CarreraId",
                table: "Alumno",
                column: "CarreraId",
                principalTable: "Carreras",
                principalColumn: "CarreraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alumno_Carreras_CarreraId",
                table: "Alumno");

            migrationBuilder.DropIndex(
                name: "IX_Alumno_CarreraId",
                table: "Alumno");

            migrationBuilder.DropColumn(
                name: "CarreraId",
                table: "Alumno");
        }
    }
}
