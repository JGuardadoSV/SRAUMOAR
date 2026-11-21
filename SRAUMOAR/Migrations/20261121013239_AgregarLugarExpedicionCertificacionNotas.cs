using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarLugarExpedicionCertificacionNotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[] { 79, "LugarExpedicion", "Lugar de expedición de la certificación de notas", "CertificacionNotas", "el distrito de Tejutla, municipio de Chalatenango Centro, departamento de Chalatenango" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 79);
        }
    }
}
