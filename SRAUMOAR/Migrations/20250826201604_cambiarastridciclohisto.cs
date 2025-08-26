using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class cambiarastridciclohisto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HistorialCiclo_Ciclos_CicloId",
                table: "HistorialCiclo");

            migrationBuilder.DropIndex(
                name: "IX_HistorialCiclo_CicloId",
                table: "HistorialCiclo");

            migrationBuilder.DropColumn(
                name: "CicloId",
                table: "HistorialCiclo");

            migrationBuilder.AddColumn<string>(
                name: "CicloTexto",
                table: "HistorialCiclo",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CicloTexto",
                table: "HistorialCiclo");

            migrationBuilder.AddColumn<int>(
                name: "CicloId",
                table: "HistorialCiclo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialCiclo_CicloId",
                table: "HistorialCiclo",
                column: "CicloId");

            migrationBuilder.AddForeignKey(
                name: "FK_HistorialCiclo_Ciclos_CicloId",
                table: "HistorialCiclo",
                column: "CicloId",
                principalTable: "Ciclos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
