using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class IntegracionFacturacionElectronica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmbienteFiscal",
                table: "Factura",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveIdempotencia",
                table: "Factura",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorreoReceptor",
                table: "Factura",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoTransmision",
                table: "Factura",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Preparado");

            migrationBuilder.AddColumn<int>(
                name: "FacturaOriginalId",
                table: "Factura",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAceptacion",
                table: "Factura",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAnulacion",
                table: "Factura",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntentosTransmision",
                table: "Factura",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoNota",
                table: "Factura",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoNota",
                table: "Factura",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RespuestaHacienda",
                table: "Factura",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoDteCodigo",
                table: "Factura",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Establecimiento",
                table: "DteCorrelativos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "M001");

            migrationBuilder.AddColumn<string>(
                name: "PuntoVenta",
                table: "DteCorrelativos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "P001");

            migrationBuilder.CreateTable(
                name: "CatalogosFe",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoCatalogo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ValorAuxiliar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogosFe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ColaCorreosDte",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: true),
                    TipoCorreo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Destinatario = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Copias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Asunto = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CuerpoHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JsonAdjunto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NombreJson = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    PdfAdjunto = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    NombrePdf = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CodigoGeneracion = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Sello = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    UltimoError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProximoIntento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcesadoEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BloqueadoHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaveIdempotencia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColaCorreosDte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ColaCorreosDte_Factura_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Factura",
                        principalColumn: "FacturaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionFacturacionElectronica",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmbienteFiscal = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Habilitado = table.Column<bool>(type: "bit", nullable: false),
                    EsAmbienteFiscalActivo = table.Column<bool>(type: "bit", nullable: false),
                    UrlAutenticacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UrlRecepcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UrlAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UrlContingencia = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UrlConsulta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UrlApiExterna = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UrlFirmador = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsuarioHacienda = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CredencialApiCifrada = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordCertificadoCifrada = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RutaCertificado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CodigoEstablecimientoMh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodigoEstablecimiento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodigoPuntoVentaMh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodigoPuntoVenta = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NitEmisor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NrcEmisor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NombreEmisor = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    NombreComercial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CodigoActividad = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DescripcionActividad = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TipoEstablecimiento = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TipoEstablecimientoDescripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CodigoDepartamento = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DepartamentoDescripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CodigoMunicipio = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    MunicipioDescripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CodigoDistrito = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DistritoDescripcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ComplementoDireccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TelefonoEmisor = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CorreoEmisor = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    UsarFirmadorExterno = table.Column<bool>(type: "bit", nullable: false),
                    UsarApiExterna = table.Column<bool>(type: "bit", nullable: false),
                    TimeoutSegundos = table.Column<int>(type: "int", nullable: false),
                    MaxReintentos = table.Column<int>(type: "int", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionFacturacionElectronica", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionSmtp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Servidor = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Puerto = table.Column<int>(type: "int", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    PasswordCifrada = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailRemitente = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NombreRemitente = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CopiaOculta = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UsarSsl = table.Column<bool>(type: "bit", nullable: false),
                    UsarTls = table.Column<bool>(type: "bit", nullable: false),
                    MaxReintentos = table.Column<int>(type: "int", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionSmtp", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DteEventos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: false),
                    TipoEvento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AmbienteFiscal = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CodigoEvento = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    JsonEvento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelloRecepcion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Respuesta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DteEventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DteEventos_Factura_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Factura",
                        principalColumn: "FacturaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DteTransmisiones",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacturaId = table.Column<int>(type: "int", nullable: true),
                    TipoOperacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AmbienteFiscal = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CodigoGeneracion = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    JsonEnviado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelloRecepcion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Respuesta = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DteTransmisiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DteTransmisiones_Factura_FacturaId",
                        column: x => x.FacturaId,
                        principalTable: "Factura",
                        principalColumn: "FacturaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ConfiguracionFacturacionElectronica",
                columns: new[] { "Id", "AmbienteFiscal", "CodigoActividad", "CodigoDepartamento", "CodigoDistrito", "CodigoEstablecimiento", "CodigoEstablecimientoMh", "CodigoMunicipio", "CodigoPuntoVenta", "CodigoPuntoVentaMh", "ComplementoDireccion", "CorreoEmisor", "CredencialApiCifrada", "DepartamentoDescripcion", "DescripcionActividad", "DistritoDescripcion", "EsAmbienteFiscalActivo", "FechaActualizacion", "Habilitado", "MaxReintentos", "MunicipioDescripcion", "NitEmisor", "NombreComercial", "NombreEmisor", "NrcEmisor", "PasswordCertificadoCifrada", "RutaCertificado", "TelefonoEmisor", "TimeoutSegundos", "TipoEstablecimiento", "TipoEstablecimientoDescripcion", "UrlAnulacion", "UrlApiExterna", "UrlAutenticacion", "UrlConsulta", "UrlContingencia", "UrlFirmador", "UrlRecepcion", "UsarApiExterna", "UsarFirmadorExterno", "UsuarioHacienda" },
                values: new object[,]
                {
                    { 1, "00", "85499", "04", "33", "M001", "M001", "35", "P001", "P001", "KM 52 1/2 CARRETERA TEJUTLA, CHALATENANGO", "umoar@enviosdte.email", null, "Chalatenango", "Enseñanza superior universitaria", "TEJUTLA", false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, 3, "CHALATENANGO CENTRO", "04331410931010", "UMOAR", "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO", "1029819", null, null, "23021800", 60, "02", "Casa Matriz", null, null, null, null, null, null, null, false, false, null },
                    { 2, "01", "85499", "04", "33", "M001", "M001", "35", "P001", "P001", "KM 52 1/2 CARRETERA TEJUTLA, CHALATENANGO", "umoar@enviosdte.email", null, "Chalatenango", "Enseñanza superior universitaria", "TEJUTLA", false, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, 3, "CHALATENANGO CENTRO", "04331410931010", "UMOAR", "UNIVERSIDAD MONSEÑOR OSCAR ARNULFO ROMERO", "1029819", null, null, "23021800", 60, "02", "Casa Matriz", null, null, null, null, null, null, null, false, false, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Factura_ClaveIdempotencia",
                table: "Factura",
                column: "ClaveIdempotencia",
                unique: true,
                filter: "[ClaveIdempotencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Factura_CodigoGeneracion",
                table: "Factura",
                column: "CodigoGeneracion");

            migrationBuilder.CreateIndex(
                name: "IX_Factura_FacturaOriginalId_TipoDteCodigo",
                table: "Factura",
                columns: new[] { "FacturaOriginalId", "TipoDteCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_DteCorrelativos_TipoDocumento_Ambiente_Establecimiento_PuntoVenta",
                table: "DteCorrelativos",
                columns: new[] { "TipoDocumento", "Ambiente", "Establecimiento", "PuntoVenta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogosFe_CodigoCatalogo_Version_Codigo",
                table: "CatalogosFe",
                columns: new[] { "CodigoCatalogo", "Version", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColaCorreosDte_ClaveIdempotencia",
                table: "ColaCorreosDte",
                column: "ClaveIdempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ColaCorreosDte_FacturaId",
                table: "ColaCorreosDte",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionFacturacionElectronica_AmbienteFiscal",
                table: "ConfiguracionFacturacionElectronica",
                column: "AmbienteFiscal",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionFacturacionElectronica_EsAmbienteFiscalActivo",
                table: "ConfiguracionFacturacionElectronica",
                column: "EsAmbienteFiscalActivo",
                unique: true,
                filter: "[EsAmbienteFiscalActivo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionSmtp_Activo",
                table: "ConfiguracionSmtp",
                column: "Activo",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DteEventos_CodigoEvento_TipoEvento_AmbienteFiscal",
                table: "DteEventos",
                columns: new[] { "CodigoEvento", "TipoEvento", "AmbienteFiscal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DteEventos_FacturaId",
                table: "DteEventos",
                column: "FacturaId");

            migrationBuilder.CreateIndex(
                name: "IX_DteTransmisiones_CodigoGeneracion_TipoOperacion_AmbienteFiscal",
                table: "DteTransmisiones",
                columns: new[] { "CodigoGeneracion", "TipoOperacion", "AmbienteFiscal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DteTransmisiones_FacturaId",
                table: "DteTransmisiones",
                column: "FacturaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Factura_Factura_FacturaOriginalId",
                table: "Factura",
                column: "FacturaOriginalId",
                principalTable: "Factura",
                principalColumn: "FacturaId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Factura_Factura_FacturaOriginalId",
                table: "Factura");

            migrationBuilder.DropTable(
                name: "CatalogosFe");

            migrationBuilder.DropTable(
                name: "ColaCorreosDte");

            migrationBuilder.DropTable(
                name: "ConfiguracionFacturacionElectronica");

            migrationBuilder.DropTable(
                name: "ConfiguracionSmtp");

            migrationBuilder.DropTable(
                name: "DteEventos");

            migrationBuilder.DropTable(
                name: "DteTransmisiones");

            migrationBuilder.DropIndex(
                name: "IX_Factura_ClaveIdempotencia",
                table: "Factura");

            migrationBuilder.DropIndex(
                name: "IX_Factura_CodigoGeneracion",
                table: "Factura");

            migrationBuilder.DropIndex(
                name: "IX_Factura_FacturaOriginalId_TipoDteCodigo",
                table: "Factura");

            migrationBuilder.DropIndex(
                name: "IX_DteCorrelativos_TipoDocumento_Ambiente_Establecimiento_PuntoVenta",
                table: "DteCorrelativos");

            migrationBuilder.DropColumn(
                name: "AmbienteFiscal",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "CorreoReceptor",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "EstadoTransmision",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "FacturaOriginalId",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "FechaAceptacion",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "FechaAnulacion",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "IntentosTransmision",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "MontoNota",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "MotivoNota",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "RespuestaHacienda",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "TipoDteCodigo",
                table: "Factura");

            migrationBuilder.DropColumn(
                name: "Establecimiento",
                table: "DteCorrelativos");

            migrationBuilder.DropColumn(
                name: "PuntoVenta",
                table: "DteCorrelativos");
        }
    }
}
