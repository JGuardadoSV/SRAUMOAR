using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class detallesdecobroagregados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CobrosArancel_aranceles_ArancelId",
                table: "CobrosArancel");

            migrationBuilder.DropIndex(
                name: "IX_CobrosArancel_ArancelId",
                table: "CobrosArancel");

            migrationBuilder.DropColumn(
                name: "ArancelId",
                table: "CobrosArancel");

            migrationBuilder.RenameColumn(
                name: "Costo",
                table: "CobrosArancel",
                newName: "Total");

            migrationBuilder.CreateTable(
                name: "DetallesCobrosArancel",
                columns: table => new
                {
                    DetallesCobroArancelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CobroArancelId = table.Column<int>(type: "int", nullable: false),
                    ArancelId = table.Column<int>(type: "int", nullable: false),
                    costo = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesCobrosArancel", x => x.DetallesCobroArancelId);
                    table.ForeignKey(
                        name: "FK_DetallesCobrosArancel_CobrosArancel_CobroArancelId",
                        column: x => x.CobroArancelId,
                        principalTable: "CobrosArancel",
                        principalColumn: "CobroArancelId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesCobrosArancel_aranceles_ArancelId",
                        column: x => x.ArancelId,
                        principalTable: "aranceles",
                        principalColumn: "ArancelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesCobrosArancel_ArancelId",
                table: "DetallesCobrosArancel",
                column: "ArancelId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesCobrosArancel_CobroArancelId",
                table: "DetallesCobrosArancel",
                column: "CobroArancelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesCobrosArancel");

            migrationBuilder.RenameColumn(
                name: "Total",
                table: "CobrosArancel",
                newName: "Costo");

            migrationBuilder.AddColumn<int>(
                name: "ArancelId",
                table: "CobrosArancel",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CobrosArancel_ArancelId",
                table: "CobrosArancel",
                column: "ArancelId");

            migrationBuilder.AddForeignKey(
                name: "FK_CobrosArancel_aranceles_ArancelId",
                table: "CobrosArancel",
                column: "ArancelId",
                principalTable: "aranceles",
                principalColumn: "ArancelId");
        }
    }
}
