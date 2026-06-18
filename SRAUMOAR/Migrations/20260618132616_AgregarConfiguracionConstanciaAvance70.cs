using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionConstanciaAvance70 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[,]
                {
                    { 52, "TituloReporte", "Nombre del reporte de avance de carrera", "ConstanciaAvance70", "CONSTANCIA DE AVANCE 70%" },
                    { 53, "PorcentajeMinimo", "Porcentaje mínimo requerido para generar la constancia", "ConstanciaAvance70", "70" },
                    { 54, "EmisorCargo", "Cargo de la persona que emite la constancia", "ConstanciaAvance70", "Decano de la Facultad de Ciencias y Humanidades y Administrador en Funciones Ad Honorem de Registro Académico" },
                    { 55, "LugarInstitucion", "Ubicación institucional para el cuerpo de la constancia", "ConstanciaAvance70", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango" },
                    { 56, "Destinatario", "Institución o destino donde se presentará la constancia", "ConstanciaAvance70", "PROCURADURIA GENERAL DE LA REPÚBLICA" },
                    { 57, "Cuerpo", "Cuerpo principal de la constancia de avance", "ConstanciaAvance70", "El Infrascrito {emisorCargo}, de la Universidad Monseñor Oscar Arnulfo Romero, en el {lugarInstitucion}, HACE CONSTAR QUE: {nombrealumno}, con carnet N° {carnet}, es {alumnoalumna} de la {facultad}, en la Carrera de {carrera}, acumulando durante los ciclos académicos {cicloInicio} al ciclo {cicloFin}, {materiasAprobadas} MATERIAS APROBADAS EQUIVALENTES AL {porcentajeMinimoTexto} POR CIENTO DE LA CARGA ACADÉMICA DEL PLAN DE ESTUDIO DE LA CARRERA DE {carreraMayuscula}." },
                    { 58, "Cierre", "Párrafo de cierre de la constancia de avance", "ConstanciaAvance70", "Y para ser presentada a la {destinatario}, se le extiende la presente en el {lugarExpedicion}, {fechaExpedicion}." },
                    { 59, "LugarExpedicion", "Lugar de expedición de la constancia de avance", "ConstanciaAvance70", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango" },
                    { 60, "FirmaNombre", "Nombre del firmante de la constancia de avance", "ConstanciaAvance70", "LIC. JOSÉ AUGUSTO HERNÁNDEZ GONZÁLEZ" },
                    { 61, "FirmaCargoLinea1", "Primera línea del cargo del firmante", "ConstanciaAvance70", "DECANO DE LA FACULTAD DE CIENCIAS Y HUMANIDADES Y" },
                    { 62, "FirmaCargoLinea2", "Segunda línea del cargo del firmante", "ConstanciaAvance70", "ADMINISTRADOR EN FUNCIONES AD HONOREM" },
                    { 63, "FirmaCargoLinea3", "Tercera línea del cargo del firmante", "ConstanciaAvance70", "DE REGISTRO ACADÉMICO" },
                    { 64, "ElaboradoPor", "Texto de elaboración al pie de la constancia", "ConstanciaAvance70", "ELABORADO POR: MTRC" },
                    { 65, "LeyendaValidez", "Leyenda de validez al pie de la constancia", "ConstanciaAvance70", "ESTA CONSTANCIA NO ES VÁLIDA, SIN FIRMAS Y SELLOS DE ESTA UNIVERSIDAD" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 65);
        }
    }
}
