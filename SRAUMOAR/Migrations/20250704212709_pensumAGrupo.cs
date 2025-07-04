using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class pensumAGrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PensumId",
                table: "grupos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_grupos_PensumId",
                table: "grupos",
                column: "PensumId");

            migrationBuilder.AddForeignKey(
                name: "FK_grupos_Pensum_PensumId",
                table: "grupos",
                column: "PensumId",
                principalTable: "Pensum",
                principalColumn: "PensumId",
                onDelete: ReferentialAction.NoAction); // Cambiado de Cascade a NoAction
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_grupos_Pensum_PensumId",
                table: "grupos");

            migrationBuilder.DropIndex(
                name: "IX_grupos_PensumId",
                table: "grupos");

            migrationBuilder.DropColumn(
                name: "PensumId",
                table: "grupos");
        }
    }
}
