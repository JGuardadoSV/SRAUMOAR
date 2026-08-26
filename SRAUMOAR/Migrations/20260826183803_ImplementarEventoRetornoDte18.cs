using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class ImplementarEventoRetornoDte18 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoRetorno",
                table: "Factura",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoRetorno",
                table: "Factura",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DteRetornoDetalle",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: false),
                    NumeroItemOriginal = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CantidadOriginal = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    CantidadRetornada = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    ClasificacionFiscal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DteRetornoDetalle", x => x.Id);
                    table.CheckConstraint("CK_DteRetornoDetalle_CantidadOriginal", "[CantidadOriginal] > 0");
                    table.CheckConstraint("CK_DteRetornoDetalle_CantidadRetornada", "[CantidadRetornada] > 0 AND [CantidadRetornada] <= [CantidadOriginal]");
                    table.CheckConstraint("CK_DteRetornoDetalle_PrecioUnitario", "[PrecioUnitario] > 0");
                    table.ForeignKey(
                        name: "FK_DteRetornoDetalle_Factura_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Factura",
                        principalColumn: "FacturaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Factura_TipoRetorno",
                table: "Factura",
                sql: "[TipoRetorno] IS NULL OR [TipoRetorno] IN ('1','2','3','4')");

            migrationBuilder.CreateIndex(
                name: "IX_DteRetornoDetalle_FacturaId_NumeroItemOriginal",
                table: "DteRetornoDetalle",
                columns: new[] { "FacturaId", "NumeroItemOriginal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DteRetornoDetalle");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Factura_TipoRetorno",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "MotivoRetorno",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "TipoRetorno",
                table: "Factura");
        }
    }
}
