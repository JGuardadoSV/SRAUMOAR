using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class PermisosPorRolEditable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModulosPermiso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulosPermiso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermisosModuloRol",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuloPermisoId = table.Column<int>(type: "int", nullable: false),
                    NivelAccesoId = table.Column<int>(type: "int", nullable: false),
                    PuedeVer = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosModuloRol", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermisosModuloRol_ModulosPermiso_ModuloPermisoId",
                        column: x => x.ModuloPermisoId,
                        principalTable: "ModulosPermiso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PermisosModuloRol_NivelAcceso_NivelAccesoId",
                        column: x => x.NivelAccesoId,
                        principalTable: "NivelAcceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModulosPermiso_Codigo",
                table: "ModulosPermiso",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermisosModuloRol_ModuloPermisoId_NivelAccesoId",
                table: "PermisosModuloRol",
                columns: new[] { "ModuloPermisoId", "NivelAccesoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermisosModuloRol_NivelAccesoId",
                table: "PermisosModuloRol",
                column: "NivelAccesoId");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM NivelAcceso WHERE Nombre = 'Contabilidad')
BEGIN
    INSERT INTO NivelAcceso (Nombre) VALUES ('Contabilidad');
END;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'TABLAS_GENERALES')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('TABLAS_GENERALES', 'Tablas generales', 10, 1);

IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'GESTION_ACADEMICA')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('GESTION_ACADEMICA', 'Gestión académica (grupos, notas)', 20, 1);

IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'MATRICULA_INSCRIPCION')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('MATRICULA_INSCRIPCION', 'Matrícula e inscripción', 30, 1);

IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'COLECTURIA')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('COLECTURIA', 'Colecturía (cobros y facturas)', 40, 1);

IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'REPORTE_INSOLVENTES')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('REPORTE_INSOLVENTES', 'Reporte de insolventes', 50, 1);

IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'SEGURIDAD')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('SEGURIDAD', 'Seguridad (usuarios y contraseñas)', 60, 1);

IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'PERMISOS_ROL')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('PERMISOS_ROL', 'Permisos por rol', 70, 1);

IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'PORTAL_DOCENTE')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('PORTAL_DOCENTE', 'Portal docente', 80, 1);

IF NOT EXISTS (SELECT 1 FROM ModulosPermiso WHERE Codigo = 'PORTAL_ESTUDIANTE')
INSERT INTO ModulosPermiso (Codigo, Nombre, Orden, Activo) VALUES ('PORTAL_ESTUDIANTE', 'Portal estudiante', 90, 1);
");

            migrationBuilder.Sql(@"
;WITH BasePermisos AS (
    SELECT 'TABLAS_GENERALES' AS Codigo, 'Administrador' AS Rol, CAST(1 AS bit) AS PuedeVer UNION ALL
    SELECT 'TABLAS_GENERALES', 'Administracion', 1 UNION ALL
    SELECT 'GESTION_ACADEMICA', 'Administrador', 1 UNION ALL
    SELECT 'GESTION_ACADEMICA', 'Administracion', 1 UNION ALL
    SELECT 'GESTION_ACADEMICA', 'Docentes', 1 UNION ALL
    SELECT 'MATRICULA_INSCRIPCION', 'Administrador', 1 UNION ALL
    SELECT 'MATRICULA_INSCRIPCION', 'Administracion', 1 UNION ALL
    SELECT 'COLECTURIA', 'Administrador', 1 UNION ALL
    SELECT 'COLECTURIA', 'Administracion', 1 UNION ALL
    SELECT 'COLECTURIA', 'Contabilidad', 1 UNION ALL
    SELECT 'REPORTE_INSOLVENTES', 'Administrador', 1 UNION ALL
    SELECT 'REPORTE_INSOLVENTES', 'Administracion', 1 UNION ALL
    SELECT 'REPORTE_INSOLVENTES', 'Contabilidad', 1 UNION ALL
    SELECT 'SEGURIDAD', 'Administrador', 1 UNION ALL
    SELECT 'SEGURIDAD', 'Administracion', 1 UNION ALL
    SELECT 'PERMISOS_ROL', 'Administrador', 1 UNION ALL
    SELECT 'PERMISOS_ROL', 'Administracion', 1 UNION ALL
    SELECT 'PERMISOS_ROL', 'Contabilidad', 1 UNION ALL
    SELECT 'PORTAL_DOCENTE', 'Docentes', 1 UNION ALL
    SELECT 'PORTAL_ESTUDIANTE', 'Estudiantes', 1
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermisosModuloRol");

            migrationBuilder.DropTable(
                name: "ModulosPermiso");
        }
    }
}
