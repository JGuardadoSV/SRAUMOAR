using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class modmateriasgrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Horario",
                table: "GruposMaterias");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraFin",
                table: "GruposMaterias",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraInicio",
                table: "GruposMaterias",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoraFin",
                table: "GruposMaterias");

            migrationBuilder.DropColumn(
                name: "HoraInicio",
                table: "GruposMaterias");

            migrationBuilder.AddColumn<string>(
                name: "Horario",
                table: "GruposMaterias",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
