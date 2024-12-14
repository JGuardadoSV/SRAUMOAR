using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class modinscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inscripciones_Carreras_CarreraId",
                table: "Inscripciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Inscripciones_Ciclos_CicloId",
                table: "Inscripciones");

            migrationBuilder.DropIndex(
                name: "IX_Inscripciones_CarreraId",
                table: "Inscripciones");

            migrationBuilder.DropColumn(
                name: "CarreraId",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<int>(
                name: "CarreraId",
                table: "Inscripciones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Inscripciones_CarreraId",
                table: "Inscripciones",
                column: "CarreraId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inscripciones_Carreras_CarreraId",
                table: "Inscripciones",
                column: "CarreraId",
                principalTable: "Carreras",
                principalColumn: "CarreraId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Inscripciones_Ciclos_CicloId",
                table: "Inscripciones",
                column: "CicloId",
                principalTable: "Ciclos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
