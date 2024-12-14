using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class modinscripcion2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inscripciones_grupos_GrupoId",
                table: "Inscripciones");

            migrationBuilder.RenameColumn(
                name: "GrupoId",
                table: "Inscripciones",
                newName: "CicloId");

            migrationBuilder.RenameIndex(
                name: "IX_Inscripciones_GrupoId",
                table: "Inscripciones",
                newName: "IX_Inscripciones_CicloId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inscripciones_Ciclos_CicloId",
                table: "Inscripciones",
                column: "CicloId",
                principalTable: "Ciclos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inscripciones_Ciclos_CicloId",
                table: "Inscripciones");

            migrationBuilder.RenameColumn(
                name: "CicloId",
                table: "Inscripciones",
                newName: "GrupoId");

            migrationBuilder.RenameIndex(
                name: "IX_Inscripciones_CicloId",
                table: "Inscripciones",
                newName: "IX_Inscripciones_GrupoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inscripciones_grupos_GrupoId",
                table: "Inscripciones",
                column: "GrupoId",
                principalTable: "grupos",
                principalColumn: "GrupoId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
