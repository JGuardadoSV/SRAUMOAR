using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class HistorialLibrePensum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ajustar relación HistorialCiclo - Pensum para permitir null y agregar nombre libre

            migrationBuilder.DropForeignKey(
                name: "FK_HistorialCiclo_Pensum_PensumId",
                table: "HistorialCiclo");

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

            migrationBuilder.AddForeignKey(
                name: "FK_HistorialCiclo_Pensum_PensumId",
                table: "HistorialCiclo",
                column: "PensumId",
                principalTable: "Pensum",
                principalColumn: "PensumId",
                onDelete: ReferentialAction.Restrict);

            // Ajustar relación HistorialMateria - Materias para permitir materias libres

            migrationBuilder.DropForeignKey(
                name: "FK_HistorialMateria_Materias_MateriaId",
                table: "HistorialMateria");

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

            migrationBuilder.AddForeignKey(
                name: "FK_HistorialMateria_Materias_MateriaId",
                table: "HistorialMateria",
                column: "MateriaId",
                principalTable: "Materias",
                principalColumn: "MateriaId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HistorialCiclo_Pensum_PensumId",
                table: "HistorialCiclo");

            migrationBuilder.DropForeignKey(
                name: "FK_HistorialMateria_Materias_MateriaId",
                table: "HistorialMateria");

            migrationBuilder.DropColumn(
                name: "PensumNombreLibre",
                table: "HistorialCiclo");

            migrationBuilder.DropColumn(
                name: "MateriaCodigoLibre",
                table: "HistorialMateria");

            migrationBuilder.DropColumn(
                name: "MateriaNombreLibre",
                table: "HistorialMateria");

            migrationBuilder.DropColumn(
                name: "MateriaUnidadesValorativasLibre",
                table: "HistorialMateria");

            migrationBuilder.AlterColumn<int>(
                name: "PensumId",
                table: "HistorialCiclo",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MateriaId",
                table: "HistorialMateria",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HistorialCiclo_Pensum_PensumId",
                table: "HistorialCiclo",
                column: "PensumId",
                principalTable: "Pensum",
                principalColumn: "PensumId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HistorialMateria_Materias_MateriaId",
                table: "HistorialMateria",
                column: "MateriaId",
                principalTable: "Materias",
                principalColumn: "MateriaId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}


