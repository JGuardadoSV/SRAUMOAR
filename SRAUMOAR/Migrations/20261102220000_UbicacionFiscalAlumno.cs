using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations;

public partial class UbicacionFiscalAlumno : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Sin inferencias ni backfill desde MunicipioId: los alumnos existentes deben confirmar su ubicación.
        migrationBuilder.AddColumn<string>(name: "CodigoDepartamentoFiscal", table: "Alumno", type: "nvarchar(2)", maxLength: 2, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CodigoMunicipioFiscal", table: "Alumno", type: "nvarchar(2)", maxLength: 2, nullable: true);
        migrationBuilder.AddColumn<string>(name: "CodigoDistritoFiscal", table: "Alumno", type: "nvarchar(2)", maxLength: 2, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CodigoDepartamentoFiscal", table: "Alumno");
        migrationBuilder.DropColumn(name: "CodigoMunicipioFiscal", table: "Alumno");
        migrationBuilder.DropColumn(name: "CodigoDistritoFiscal", table: "Alumno");
    }
}
