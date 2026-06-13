using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionReporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionesReportes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reporte = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesReportes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[,]
                {
                    { 1, "DireccionLinea1", "Primera línea de dirección de la universidad", "CertificacionNotas", "" },
                    { 2, "DireccionLinea2", "Segunda línea de dirección de la universidad", "CertificacionNotas", "" },
                    { 3, "DireccionLinea3", "Tercera línea de dirección de la universidad", "CertificacionNotas", "" },
                    { 4, "Introduccion", "Párrafo de texto introductorio en negrita", "CertificacionNotas", "" },
                    { 5, "FirmaNombre", "Nombre del firmante en el pie de página", "CertificacionNotas", "" },
                    { 6, "FirmaCargo", "Cargo del firmante en el pie de página", "CertificacionNotas", "" },
                    { 7, "FirmaSublinea", "Información adicional o segunda línea de firma (ej. Sello o Registro)", "CertificacionNotas", "" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesReportes");
        }
    }
}
