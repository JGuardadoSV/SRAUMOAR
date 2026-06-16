using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarSemillasRectora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[,]
                {
                    { 8, "RectoraNombre", "Nombre de la Rectora", "CertificacionNotas", "LICDA. CARMEN NAVAS ESCOBAR DE MEJÍA" },
                    { 9, "RectoraCargo", "Cargo de la Rectora", "CertificacionNotas", "RECTORA" },
                    { 10, "ConfrontadoPor", "Nombre de la persona que confronta", "CertificacionNotas", "LIC. JOSE AUGUSTO HERNANDEZ GONZALEZ" },
                    { 11, "RectoraCertificacion", "Texto de certificación de la Rectora", "CertificacionNotas", "La infrascrita, Rectora de la Universidad Monseñor Oscar Arnulfo Romero, certifica que la firma que aparece al pie de la certificación global de notas es auténtica y es la misma que usa el {SecretarioNombre}, {SecretarioCargo} de esta universidad." }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 11);
        }
    }
}
