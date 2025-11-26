using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class AgregarNotaRecuperacionMateriasInscritas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaRecuperacion",
                table: "MateriasInscritas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NotaRecuperacion",
                table: "MateriasInscritas",
                type: "decimal(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FechaRecuperacion",
                table: "MateriasInscritas");

            migrationBuilder.DropColumn(
                name: "NotaRecuperacion",
                table: "MateriasInscritas");
        }
    }
}
