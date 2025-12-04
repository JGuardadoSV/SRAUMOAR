using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class FinalizarHistorialLibrePensum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MateriaId",
                table: "HistorialMateria",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "MateriaCodigoLibre",
                table: "HistorialMateria",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MateriaNombreLibre",
                table: "HistorialMateria",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MateriaUnidadesValorativasLibre",
                table: "HistorialMateria",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PensumId",
                table: "HistorialCiclo",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "PensumNombreLibre",
                table: "HistorialCiclo",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MateriaCodigoLibre",
                table: "HistorialMateria");

            migrationBuilder.DropColumn(
                name: "MateriaNombreLibre",
                table: "HistorialMateria");

            migrationBuilder.DropColumn(
                name: "MateriaUnidadesValorativasLibre",
                table: "HistorialMateria");

            migrationBuilder.DropColumn(
                name: "PensumNombreLibre",
                table: "HistorialCiclo");

            migrationBuilder.AlterColumn<int>(
                name: "MateriaId",
                table: "HistorialMateria",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PensumId",
                table: "HistorialCiclo",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
