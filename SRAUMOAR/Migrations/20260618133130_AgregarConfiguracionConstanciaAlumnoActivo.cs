using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionConstanciaAlumnoActivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[,]
                {
                    { 66, "TituloReporte", "Nombre del reporte de alumno activo", "ConstanciaAlumnoActivo", "ALUMNO ACTIVO" },
                    { 67, "EmisorCargo", "Cargo de la persona que emite la constancia", "ConstanciaAlumnoActivo", "Decano de la Facultad de Ciencias y Humanidades y Administrador en Funciones Ad Honorem de Registro Académico" },
                    { 68, "LugarInstitucion", "Ubicación institucional para el cuerpo de la constancia", "ConstanciaAlumnoActivo", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango" },
                    { 69, "Destinatario", "Institución o destino donde se presentará la constancia", "ConstanciaAlumnoActivo", "AFP CRECER" },
                    { 70, "Cuerpo", "Cuerpo principal de la constancia de alumno activo", "ConstanciaAlumnoActivo", "El Infrascrito {emisorCargo}, de la Universidad Monseñor Oscar Arnulfo Romero, en el {lugarInstitucion}, HACE CONSTAR QUE: {nombrealumno}, con carnet N°. {carnet}, es {alumnoalumna} {activoactiva} DE {cicloRomano} CICLO, de la {facultad}, en la Carrera de {carrera}, del Ciclo Académico {cicloAcademico}, el cual dio inicio el día {fechaInicioCiclo} y finalizará el día {fechaFinCiclo}." },
                    { 71, "Cierre", "Párrafo de cierre de la constancia de alumno activo", "ConstanciaAlumnoActivo", "Y para ser presentada a la {destinatario}, se le extiende la presente en el {lugarExpedicion}, {fechaExpedicion}." },
                    { 72, "LugarExpedicion", "Lugar de expedición de la constancia de alumno activo", "ConstanciaAlumnoActivo", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango" },
                    { 73, "FirmaNombre", "Nombre del firmante de la constancia de alumno activo", "ConstanciaAlumnoActivo", "LIC. JOSÉ AUGUSTO HERNÁNDEZ GONZÁLEZ" },
                    { 74, "FirmaCargoLinea1", "Primera línea del cargo del firmante", "ConstanciaAlumnoActivo", "DECANO DE LA FACULTAD DE CIENCIAS Y HUMANIDADES Y" },
                    { 75, "FirmaCargoLinea2", "Segunda línea del cargo del firmante", "ConstanciaAlumnoActivo", "ADMINISTRADOR EN FUNCIONES AD HONOREM" },
                    { 76, "FirmaCargoLinea3", "Tercera línea del cargo del firmante", "ConstanciaAlumnoActivo", "DE REGISTRO ACADÉMICO" },
                    { 77, "ElaboradoPor", "Texto de elaboración al pie de la constancia", "ConstanciaAlumnoActivo", "ELABORADO POR: MTRC" },
                    { 78, "LeyendaValidez", "Leyenda de validez al pie de la constancia", "ConstanciaAlumnoActivo", "ESTA CONSTANCIA NO ES VÁLIDA, SIN FIRMAS Y SELLOS DE ESTA UNIVERSIDAD" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 78);
        }
    }
}
