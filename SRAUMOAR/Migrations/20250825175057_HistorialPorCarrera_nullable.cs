using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class HistorialPorCarrera_nullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Asegurar que todos los valores existentes de CarreraId sean nulos o válidos
            migrationBuilder.Sql(@"
                UPDATE HA
                SET CarreraId = NULL
                FROM HistorialAcademico HA
                LEFT JOIN Carreras C ON C.CarreraId = HA.CarreraId
                WHERE HA.CarreraId IS NOT NULL AND C.CarreraId IS NULL;
            ");

            // Agregar la restricción de FK después de limpiar datos
            migrationBuilder.AddForeignKey(
                name: "FK_HistorialAcademico_Carreras_CarreraId",
                table: "HistorialAcademico",
                column: "CarreraId",
                principalTable: "Carreras",
                principalColumn: "CarreraId",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HistorialAcademico_Carreras_CarreraId",
                table: "HistorialAcademico");
        }
    }
}
