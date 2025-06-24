using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class facturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoGeneracion",
                table: "CobrosArancel",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Factura",
                columns: table => new
                {
                    FacturaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JsonDte = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Anulada = table.Column<bool>(type: "bit", nullable: false),
                    SelloRecepcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JsonAnulacion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SelloAnulacion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CodigoGeneracion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NumeroControl = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TipoDTE = table.Column<int>(type: "int", nullable: false),
                    TotalGravado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalExento = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPagar = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalIva = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factura", x => x.FacturaId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Factura");

            migrationBuilder.DropColumn(
                name: "CodigoGeneracion",
                table: "CobrosArancel");
        }
    }
}
