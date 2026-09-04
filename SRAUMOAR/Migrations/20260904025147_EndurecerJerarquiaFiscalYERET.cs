using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class EndurecerJerarquiaFiscalYERET : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM [Factura]
                    WHERE [TipoDteCodigo] = '18'
                      AND ([TipoRetorno] IS NULL OR [TipoRetorno] NOT IN ('1','2','3','4')
                           OR [MotivoRetorno] IS NULL OR LEN(LTRIM(RTRIM([MotivoRetorno]))) = 0))
                    THROW 50002, 'Hay ERET históricos sin tipo o motivo. Corrija esos datos antes de aplicar esta migración.', 1;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Factura_EretTrazabilidad",
                table: "Factura");

            migrationBuilder.DropIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo_AmbitoDepartamento_AmbitoMunicipio",
                table: "CatalogosFe");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Factura_EretTrazabilidad",
                table: "Factura",
                sql: "[TipoDteCodigo] IS NULL OR [TipoDteCodigo] <> '18' OR ([TipoRetorno] IS NOT NULL AND [TipoRetorno] IN ('1','2','3','4') AND [MotivoRetorno] IS NOT NULL AND LEN(LTRIM(RTRIM([MotivoRetorno]))) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo",
                table: "CatalogosFe",
                columns: new[] { "CodigoCatalogo", "Version", "Codigo" },
                unique: true,
                filter: "[CodigoCatalogo] NOT IN ('CAT-008','CAT-013')");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo_AmbitoDepartamento",
                table: "CatalogosFe",
                columns: new[] { "CodigoCatalogo", "Version", "Codigo", "AmbitoDepartamento" },
                unique: true,
                filter: "[CodigoCatalogo] = 'CAT-013'");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo_AmbitoDepartamento_AmbitoMunicipio",
                table: "CatalogosFe",
                columns: new[] { "CodigoCatalogo", "Version", "Codigo", "AmbitoDepartamento", "AmbitoMunicipio" },
                unique: true,
                filter: "[CodigoCatalogo] = 'CAT-008'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CatalogosFe_AmbitoTerritorial",
                table: "CatalogosFe",
                sql: "([CodigoCatalogo] = 'CAT-008' AND (([AmbitoDepartamento] = '' AND [AmbitoMunicipio] = '') OR ([AmbitoDepartamento] <> '' AND [AmbitoMunicipio] <> ''))) OR ([CodigoCatalogo] = 'CAT-013' AND (([AmbitoDepartamento] = '' AND [AmbitoMunicipio] = '') OR ([AmbitoDepartamento] <> '' AND [AmbitoMunicipio] = [Codigo]))) OR ([CodigoCatalogo] NOT IN ('CAT-008','CAT-013') AND [AmbitoDepartamento] = '' AND [AmbitoMunicipio] = '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Factura_EretTrazabilidad",
                table: "Factura");

            migrationBuilder.DropIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo",
                table: "CatalogosFe");

            migrationBuilder.DropIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo_AmbitoDepartamento",
                table: "CatalogosFe");

            migrationBuilder.DropIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo_AmbitoDepartamento_AmbitoMunicipio",
                table: "CatalogosFe");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CatalogosFe_AmbitoTerritorial",
                table: "CatalogosFe");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Factura_EretTrazabilidad",
                table: "Factura",
                sql: "[TipoDteCodigo] <> '18' OR ([TipoRetorno] IN ('1','2','3','4') AND LEN(LTRIM(RTRIM([MotivoRetorno]))) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo_AmbitoDepartamento_AmbitoMunicipio",
                table: "CatalogosFe",
                columns: new[] { "CodigoCatalogo", "Version", "Codigo", "AmbitoDepartamento", "AmbitoMunicipio" },
                unique: true);
        }
    }
}
