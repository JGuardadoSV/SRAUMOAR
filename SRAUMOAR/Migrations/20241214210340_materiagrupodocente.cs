using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class materiagrupodocente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocenteId",
                table: "GruposMaterias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_GruposMaterias_DocenteId",
                table: "GruposMaterias",
                column: "DocenteId");

            migrationBuilder.AddForeignKey(
                name: "FK_GruposMaterias_Docentes_DocenteId",
                table: "GruposMaterias",
                column: "DocenteId",
                principalTable: "Docentes",
                principalColumn: "DocenteId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GruposMaterias_Docentes_DocenteId",
                table: "GruposMaterias");

            migrationBuilder.DropIndex(
                name: "IX_GruposMaterias_DocenteId",
                table: "GruposMaterias");

            migrationBuilder.DropColumn(
                name: "DocenteId",
                table: "GruposMaterias");
        }
    }
}
