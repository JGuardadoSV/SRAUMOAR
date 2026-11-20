using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class ClientesRecurrentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientesRecurrentes",
                columns: table => new
                {
                    ClienteRecurrenteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nrc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NombreComercial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodActividad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DescActividad = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CodigoDepartamento = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CodigoMunicipio = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CodigoDistrito = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Complemento = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    CodigoDomicilioFiscal = table.Column<int>(type: "int", nullable: true),
                    GranContribuyente = table.Column<bool>(type: "bit", nullable: false),
                    RetencionRenta = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientesRecurrentes", x => x.ClienteRecurrenteId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientesRecurrentes");
        }
    }
}
