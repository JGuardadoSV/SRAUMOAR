using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIvaRetenidoFactura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IvaRetenido",
                table: "Factura",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Recuperar las retenciones ya emitidas desde el JSON fiscal almacenado.
            migrationBuilder.Sql("""
                UPDATE Factura
                SET IvaRetenido = COALESCE(TRY_CONVERT(decimal(18,2), JSON_VALUE(JsonDte, '$.resumen.ivaRete')), 0)
                WHERE ISJSON(JsonDte) = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IvaRetenido",
                table: "Factura");
        }
    }
}
