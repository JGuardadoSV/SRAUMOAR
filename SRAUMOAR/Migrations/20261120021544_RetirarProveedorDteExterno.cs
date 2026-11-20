using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class RetirarProveedorDteExterno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UrlApiExterna",
                table: "ConfiguracionFacturacionElectronica");

            migrationBuilder.DropColumn(
                name: "UrlFirmador",
                table: "ConfiguracionFacturacionElectronica");

            migrationBuilder.DropColumn(
                name: "UsarApiExterna",
                table: "ConfiguracionFacturacionElectronica");

            migrationBuilder.DropColumn(
                name: "UsarFirmadorExterno",
                table: "ConfiguracionFacturacionElectronica");

            migrationBuilder.RenameColumn(
                name: "CredencialApiCifrada",
                table: "ConfiguracionFacturacionElectronica",
                newName: "CredencialHaciendaCifrada");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CredencialHaciendaCifrada",
                table: "ConfiguracionFacturacionElectronica",
                newName: "CredencialApiCifrada");

            migrationBuilder.AddColumn<string>(
                name: "UrlApiExterna",
                table: "ConfiguracionFacturacionElectronica",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UrlFirmador",
                table: "ConfiguracionFacturacionElectronica",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsarApiExterna",
                table: "ConfiguracionFacturacionElectronica",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UsarFirmadorExterno",
                table: "ConfiguracionFacturacionElectronica",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "ConfiguracionFacturacionElectronica",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "UrlApiExterna", "UrlFirmador", "UsarApiExterna", "UsarFirmadorExterno" },
                values: new object[] { null, null, false, false });

            migrationBuilder.UpdateData(
                table: "ConfiguracionFacturacionElectronica",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UrlApiExterna", "UrlFirmador", "UsarApiExterna", "UsarFirmadorExterno" },
                values: new object[] { null, null, false, false });
        }
    }
}
