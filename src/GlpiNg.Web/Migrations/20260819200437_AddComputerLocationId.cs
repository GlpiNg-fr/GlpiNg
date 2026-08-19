using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddComputerLocationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Computers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Computers_LocationId",
                table: "Computers",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Computers_DropdownItems_LocationId",
                table: "Computers",
                column: "LocationId",
                principalTable: "DropdownItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Computers_DropdownItems_LocationId",
                table: "Computers");

            migrationBuilder.DropIndex(
                name: "IX_Computers_LocationId",
                table: "Computers");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Computers");
        }
    }
}
