﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRAUMOAR.Migrations
{
    /// <inheritdoc />
    public partial class quitarpensumdegrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_grupos_Pensum_PensumIdG_FK",
                table: "grupos");

            migrationBuilder.DropIndex(
                name: "IX_grupos_PensumIdG_FK",
                table: "grupos");

            migrationBuilder.DropColumn(
                name: "PensumIdG_FK",
                table: "grupos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PensumIdG_FK",
                table: "grupos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_grupos_PensumIdG_FK",
                table: "grupos",
                column: "PensumIdG_FK");

            migrationBuilder.AddForeignKey(
                name: "FK_grupos_Pensum_PensumIdG_FK",
                table: "grupos",
                column: "PensumIdG_FK",
                principalTable: "Pensum",
                principalColumn: "PensumId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
