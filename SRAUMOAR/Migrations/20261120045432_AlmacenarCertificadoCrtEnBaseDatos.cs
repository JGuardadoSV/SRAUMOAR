using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AlmacenarCertificadoCrtEnBaseDatos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CertificadoCrt",
                table: "ConfiguracionFacturacionElectronica",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ConfiguracionFacturacionElectronica",
                keyColumn: "Id",
                keyValue: 1,
                column: "CertificadoCrt",
                value: null);

            migrationBuilder.UpdateData(
                table: "ConfiguracionFacturacionElectronica",
                keyColumn: "Id",
                keyValue: 2,
                column: "CertificadoCrt",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificadoCrt",
                table: "ConfiguracionFacturacionElectronica");
        }
    }
}
