using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEsInternoEquivalencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsEquivalenciaInterna",
                table: "HistorialMateria",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EsInterno",
                table: "EstudiosEquivalencia",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsEquivalenciaInterna",
                table: "HistorialMateria");

            migrationBuilder.DropColumn(
                name: "EsInterno",
                table: "EstudiosEquivalencia");
        }
    }
}
