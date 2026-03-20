using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarModuloDeserciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CausasDesercion",
                columns: table => new
                {
                    CausaDesercionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CausasDesercion", x => x.CausaDesercionId);
                });

            migrationBuilder.CreateTable(
                name: "DesercionesAlumno",
                columns: table => new
                {
                    DesercionAlumnoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlumnoId = table.Column<int>(type: "int", nullable: false),
                    CicloId = table.Column<int>(type: "int", nullable: false),
                    CausaDesercionId = table.Column<int>(type: "int", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesercionesAlumno", x => x.DesercionAlumnoId);
                    table.ForeignKey(
                        name: "FK_DesercionesAlumno_Alumno_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumno",
                        principalColumn: "AlumnoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DesercionesAlumno_CausasDesercion_CausaDesercionId",
                        column: x => x.CausaDesercionId,
                        principalTable: "CausasDesercion",
                        principalColumn: "CausaDesercionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DesercionesAlumno_Ciclos_CicloId",
                        column: x => x.CicloId,
                        principalTable: "Ciclos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CausasDesercion",
                columns: new[] { "CausaDesercionId", "Activo", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "Abandono" },
                    { 2, true, "Deserción" },
                    { 3, true, "Retiro de ciclo" }
                });

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'DESERCIONES')
BEGIN
    INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo)
    VALUES ('DESERCIONES', 'Registro de deserciones', 55, 1);
END;
");

            migrationBuilder.Sql(@"
;WITH BasePermisos AS (
    SELECT 'DESERCIONES' AS Codigo, 'Administrador' AS Rol, CAST(1 AS bit) AS PuedeVer UNION ALL
    SELECT 'DESERCIONES', 'Administracion', 1 UNION ALL
    SELECT 'DESERCIONES', 'Contabilidad', 1
)
INSERT INTO PermisosModuloRol (ModuloPermisoId, NivelAccesoId, PuedeVer)
SELECT m.Id, r.Id, b.PuedeVer
FROM BasePermisos b
JOIN ModulosPermiso m ON m.Codigo = b.Codigo
JOIN NivelAcceso r ON r.Nombre = b.Rol
WHERE NOT EXISTS (
    SELECT 1
    FROM PermisosModuloRol p
    WHERE p.ModuloPermisoId = m.Id AND p.NivelAccesoId = r.Id
);
");

            migrationBuilder.CreateIndex(
                name: "IX_DesercionesAlumno_AlumnoId_CicloId",
                table: "DesercionesAlumno",
                columns: new[] { "AlumnoId", "CicloId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesercionesAlumno_CausaDesercionId",
                table: "DesercionesAlumno",
                column: "CausaDesercionId");

            migrationBuilder.CreateIndex(
                name: "IX_DesercionesAlumno_CicloId",
                table: "DesercionesAlumno",
                column: "CicloId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DesercionesAlumno");

            migrationBuilder.DropTable(
                name: "CausasDesercion");

            migrationBuilder.Sql(@"
DELETE p
FROM PermisosModuloRol p
INNER JOIN ModulosPermiso m ON m.Id = p.ModuloPermisoId
WHERE m.Codigo = 'DESERCIONES';
");

            migrationBuilder.Sql(@"
DELETE FROM ModulosPermiso
WHERE Codigo = 'DESERCIONES';
");
        }
    }
}
