using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionConstanciaCursoPreUniversitario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[,]
                {
                    { 12, "TituloArea", "Título del área de la constancia", "ConstanciaCursoPreUniversitario", "RECTORIA" },
                    { 13, "TituloReporte", "Nombre del reporte", "ConstanciaCursoPreUniversitario", "CONSTANCIA DE CURSO PRE UNIVERSITARIO" },
                    { 14, "Cuerpo", "Cuerpo principal de la constancia", "ConstanciaCursoPreUniversitario", "El Infrascrito Rector de la Universidad Monseñor Oscar Arnulfo Romero, por la presente hace constar que: {nombrealumno}, realizó y aprobó su curso pre-universitario, impartido en esta institución desde el día {fechaInicioCurso} al {fechaFinCurso}." },
                    { 15, "Cierre", "Párrafo de cierre de la constancia", "ConstanciaCursoPreUniversitario", "Y para ser presentada a la Administración de Registro Académico, se extiende la presente Constancia, en {lugarExpedicion} {fechaExpedicion}." },
                    { 16, "FirmaNombre", "Nombre del firmante de la constancia", "ConstanciaCursoPreUniversitario", "" },
                    { 17, "FirmaCargo", "Cargo del firmante de la constancia", "ConstanciaCursoPreUniversitario", "RECTOR" },
                    { 18, "LugarExpedicion", "Lugar de expedición de la constancia", "ConstanciaCursoPreUniversitario", "Tejutla, Chalatenango" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 18);
        }
    }
}
