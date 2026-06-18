using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionConstanciaPracticaDocente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[,]
                {
                    { 39, "TituloReporte", "Nombre del reporte de práctica docente", "ConstanciaPracticaDocente", "CONSTANCIA PRÁCTICA DOCENTE" },
                    { 40, "FiltroMaterias", "Texto usado para identificar materias de práctica docente en el historial", "ConstanciaPracticaDocente", "PRACTICA DOCENTE" },
                    { 41, "EmisorCargo", "Cargo de la persona que emite la constancia", "ConstanciaPracticaDocente", "Decano de la Facultad de Ciencias y Humanidades y Administrador en Funciones Ad Honorem de Registro Académico" },
                    { 42, "LugarInstitucion", "Ubicación institucional para el cuerpo de la constancia", "ConstanciaPracticaDocente", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango" },
                    { 43, "Cuerpo", "Cuerpo principal de la constancia de práctica docente", "ConstanciaPracticaDocente", "El Infrascrito {emisorCargo}, de la Universidad Monseñor Oscar Arnulfo Romero, en el {lugarInstitucion}, HACE CONSTAR QUE: {nombrealumno}, con carnet N° {carnet}, es {alumnoalumna} de la {facultad}, en la Carrera de {carrera}, CURSO Y APROBO durante los ciclos académicos {cicloInicio} al ciclo {cicloFin} las asignaturas de {materiasPractica}." },
                    { 44, "Cierre", "Párrafo de cierre de la constancia de práctica docente", "ConstanciaPracticaDocente", "Y para los usos que {interesadointeresada} estime conveniente, se le extiende la presente en el {lugarExpedicion}, {fechaExpedicion}." },
                    { 45, "LugarExpedicion", "Lugar de expedición de la constancia de práctica docente", "ConstanciaPracticaDocente", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango" },
                    { 46, "FirmaNombre", "Nombre del firmante de la constancia de práctica docente", "ConstanciaPracticaDocente", "LIC. JOSÉ AUGUSTO HERNÁNDEZ GONZÁLEZ" },
                    { 47, "FirmaCargoLinea1", "Primera línea del cargo del firmante", "ConstanciaPracticaDocente", "DECANO DE LA FACULTAD DE CIENCIAS Y HUMANIDADES Y" },
                    { 48, "FirmaCargoLinea2", "Segunda línea del cargo del firmante", "ConstanciaPracticaDocente", "ADMINISTRADOR EN FUNCIONES AD HONOREM" },
                    { 49, "FirmaCargoLinea3", "Tercera línea del cargo del firmante", "ConstanciaPracticaDocente", "DE REGISTRO ACADÉMICO" },
                    { 50, "ElaboradoPor", "Texto de elaboración al pie de la constancia", "ConstanciaPracticaDocente", "ELABORADO POR: MTRC" },
                    { 51, "LeyendaValidez", "Leyenda de validez al pie de la constancia", "ConstanciaPracticaDocente", "ESTA CONSTANCIA NO ES VÁLIDA, SIN FIRMAS Y SELLOS DE ESTA UNIVERSIDAD" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 51);
        }
    }
}
