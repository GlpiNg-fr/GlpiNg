using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCartridgeLevelReading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LevelPercent",
                table: "Cartridges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LevelReadAt",
                table: "Cartridges",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LevelPercent",
                table: "Cartridges");

            migrationBuilder.DropColumn(
                name: "LevelReadAt",
                table: "Cartridges");
        }
    }
}
