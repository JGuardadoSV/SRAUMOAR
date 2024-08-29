using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class materiasgrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GruposMaterias",
                columns: table => new
                {
                    MateriasGrupoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Aula = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Horario = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MateriaId = table.Column<int>(type: "int", nullable: false),
                    GrupoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposMaterias", x => x.MateriasGrupoId);
                    table.ForeignKey(
                        name: "FK_GruposMaterias_Materias_MateriaId",
                        column: x => x.MateriaId,
                        principalTable: "Materias",
                        principalColumn: "MateriaId",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_GruposMaterias_grupos_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "grupos",
                        principalColumn: "GrupoId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GruposMaterias_GrupoId",
                table: "GruposMaterias",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposMaterias_MateriaId",
                table: "GruposMaterias",
                column: "MateriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GruposMaterias");
        }
    }
}
