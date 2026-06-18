using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionConstanciaHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ConfiguracionesReportes",
                columns: new[] { "Id", "Clave", "Descripcion", "Reporte", "Valor" },
                values: new object[,]
                {
                    { 19, "TituloReporte", "Nombre del reporte de horario", "ConstanciaHorario", "CONSTANCIA DE HORARIO" },
                    { 20, "EmisorCargo", "Cargo de la persona que emite la constancia", "ConstanciaHorario", "Decano de la Facultad de Ciencias y Humanidades y Administrador en Funciones Ad Honorem de Registro Académico" },
                    { 21, "LugarInstitucion", "Ubicación institucional para el cuerpo de la constancia", "ConstanciaHorario", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango" },
                    { 22, "Destinatario", "Institución o destino donde se presentará la constancia", "ConstanciaHorario", "PROCURADURÍA GENERAL DE LA REPÚBLICA DE EL SALVADOR" },
                    { 23, "Cuerpo", "Cuerpo principal de la constancia de horario", "ConstanciaHorario", "El Infrascrito {emisorCargo}, de la Universidad Monseñor Oscar Arnulfo Romero, en el {lugarInstitucion}, HACE CONSTAR QUE: {nombrealumno}, con carnet N° {carnet}, ES {alumnoalumna} {activoactiva} DEL {cicloRomano} CICLO, de la {facultad}, en la Carrera de {carrera}, del Ciclo Académico {cicloAcademico}, el cual dio inicio el {fechaInicioCiclo} y finalizará el día {fechaFinCiclo}, habiendo inscrito {cantidadMateriasLetras} materias en el horario siguiente:" },
                    { 24, "Cierre", "Párrafo de cierre de la constancia de horario", "ConstanciaHorario", "Y para ser presentada a la {destinatario}, se le extiende la presente en el {lugarExpedicion}, {fechaExpedicion}." },
                    { 25, "LugarExpedicion", "Lugar de expedición de la constancia de horario", "ConstanciaHorario", "Distrito de Tejutla, Municipio de Chalatenango Centro, Departamento de Chalatenango" },
                    { 26, "FirmaNombre", "Nombre del firmante de la constancia de horario", "ConstanciaHorario", "LIC. JOSÉ AUGUSTO HERNÁNDEZ GONZÁLEZ" },
                    { 27, "FirmaCargoLinea1", "Primera línea del cargo del firmante", "ConstanciaHorario", "DECANO DE LA FACULTAD DE CIENCIAS Y HUMANIDADES Y" },
                    { 28, "FirmaCargoLinea2", "Segunda línea del cargo del firmante", "ConstanciaHorario", "ADMINISTRADOR EN FUNCIONES AD HONOREM" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "ConfiguracionesReportes",
                keyColumn: "Id",
                keyValue: 28);
        }
    }
}
