using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class arancelesbecado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArancelesBecados",
                columns: table => new
                {
                    ArancelBecadoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    BecadosId = table.Column<int>(type: "int", nullable: false),
                    ArancelId = table.Column<int>(type: "int", nullable: false),
                    PrecioPersonalizado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PorcentajeDescuento = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArancelesBecados", x => x.ArancelBecadoId);
                    table.ForeignKey(
                        name: "FK_ArancelesBecados_AlumnosBecados_BecadosId",
                        column: x => x.BecadosId,
                        principalTable: "AlumnosBecados",
                        principalColumn: "BecadosId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArancelesBecados_aranceles_ArancelId",
                        column: x => x.ArancelId,
                        principalTable: "aranceles",
                        principalColumn: "ArancelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArancelesBecados_ArancelId",
                table: "ArancelesBecados",
                column: "ArancelId");

            migrationBuilder.CreateIndex(
                name: "IX_ArancelesBecados_BecadosId",
                table: "ArancelesBecados",
                column: "BecadosId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArancelesBecados");
        }
    }
}
