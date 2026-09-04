using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class CorregirJerarquiaFiscalYERET : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DteRetornoDetalle_PrecioUnitario",
                table: "DteRetornoDetalle");

            migrationBuilder.DropIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo",
                table: "CatalogosFe");

            migrationBuilder.AddColumn<string>(
                name: "AmbitoDepartamento",
                table: "CatalogosFe",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AmbitoMunicipio",
                table: "CatalogosFe",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Factura_EretTrazabilidad",
                table: "Factura",
                sql: "[TipoDteCodigo] <> '18' OR ([TipoRetorno] IN ('1','2','3','4') AND LEN(LTRIM(RTRIM([MotivoRetorno]))) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DteRetornoDetalle_PrecioUnitario",
                table: "DteRetornoDetalle",
                sql: "[PrecioUnitario] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo_AmbitoDepartamento_AmbitoMunicipio",
                table: "CatalogosFe",
                columns: new[] { "CodigoCatalogo", "Version", "Codigo", "AmbitoDepartamento", "AmbitoMunicipio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "La jerarquía territorial admite códigos repetidos y no puede volver al índice global sin perder datos. Restaure un respaldo anterior en lugar de revertir esta migración.");
        }
    }
}
