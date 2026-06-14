using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AjustarDetallesEquivalenciaOrigen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MateriaOrigenCodigo",
                table: "DetallesEquivalencia");

            migrationBuilder.DropColumn(
                name: "MateriaOrigenNombre",
                table: "DetallesEquivalencia");

            migrationBuilder.DropColumn(
                name: "NotaOrigen",
                table: "DetallesEquivalencia");

            migrationBuilder.CreateTable(
                name: "DetallesEquivalenciaOrigen",
                columns: table => new
                {
                    DetalleEquivalenciaOrigenId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DetalleEquivalenciaId = table.Column<int>(type: "int", nullable: false),
                    MateriaOrigenCodigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MateriaOrigenNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NotaOrigen = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    uv = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesEquivalenciaOrigen", x => x.DetalleEquivalenciaOrigenId);
                    table.ForeignKey(
                        name: "FK_DetallesEquivalenciaOrigen_DetallesEquivalencia_DetalleEquivalenciaId",
                        column: x => x.DetalleEquivalenciaId,
                        principalTable: "DetallesEquivalencia",
                        principalColumn: "DetalleEquivalenciaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesEquivalenciaOrigen_DetalleEquivalenciaId",
                table: "DetallesEquivalenciaOrigen",
                column: "DetalleEquivalenciaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallesEquivalenciaOrigen");

            migrationBuilder.AddColumn<string>(
                name: "MateriaOrigenCodigo",
                table: "DetallesEquivalencia",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MateriaOrigenNombre",
                table: "DetallesEquivalencia",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "NotaOrigen",
                table: "DetallesEquivalencia",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
