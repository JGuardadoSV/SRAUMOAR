using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionCartaEgreso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[,]
                {
                    { 29, "TituloReporte", "Nombre del reporte de carta de egreso", "CartaEgreso", "CARTA DE EGRESO" },
                    { 30, "EmisorCargo", "Cargo de la persona que emite la carta", "CartaEgreso", "Decano de la Facultad de Ciencias y Humanidades y Administrador en funciones ad Honorem de Registro Académico" },
                    { 31, "LugarInstitucion", "Ubicación institucional para el cuerpo de la carta", "CartaEgreso", "Jurisdicción de Tejutla, Departamento de Chalatenango" },
                    { 32, "Cuerpo", "Cuerpo principal de la carta de egreso", "CartaEgreso", "El Infrascrito {emisorCargo} de la Universidad Monseñor Oscar Arnulfo Romero, {lugarInstitucion}, DECLARA FORMALMENTE {egresadoegresada} a: {nombrealumno}, con carnet No. {carnet}, {alumnoalumna} de la {facultad}, en la carrera de {carrera}, luego de haber cumplido con los requisitos académicos establecidos en el plan de estudio: {materiasAprobadasLetras} ({materiasAprobadas}) MATERIAS APROBADAS." },
                    { 33, "Cierre", "Párrafo de cierre de la carta de egreso", "CartaEgreso", "Y para los usos que {interesadointeresada} estime conveniente, se extiende la presente en el {lugarExpedicion}, {fechaExpedicion}." },
                    { 34, "LugarExpedicion", "Lugar de expedición de la carta", "CartaEgreso", "distrito de Tejutla, municipio de Chalatenango Centro, departamento de Chalatenango" },
                    { 35, "FirmaNombre", "Nombre del firmante de la carta", "CartaEgreso", "LIC. JOSE AUGUSTO HERNANDEZ GONZALEZ" },
                    { 36, "FirmaCargoLinea1", "Primera línea del cargo del firmante", "CartaEgreso", "DECANO DE LA FACULTAD DE CIENCIAS Y HUMANIDADES Y" },
                    { 37, "FirmaCargoLinea2", "Segunda línea del cargo del firmante", "CartaEgreso", "ADMINISTRADOR EN FUNCIONES AD HONOREM DE REGISTRO" },
                    { 38, "FirmaCargoLinea3", "Tercera línea del cargo del firmante", "CartaEgreso", "ACADÉMICO" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 38);
        }
    }
}
