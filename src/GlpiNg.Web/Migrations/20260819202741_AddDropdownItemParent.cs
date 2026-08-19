using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDropdownItemParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "DropdownItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DropdownItems_ParentId",
                table: "DropdownItems",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DropdownItems_DropdownItems_ParentId",
                table: "DropdownItems",
                column: "ParentId",
                principalTable: "DropdownItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DropdownItems_DropdownItems_ParentId",
                table: "DropdownItems");

            migrationBuilder.DropIndex(
                name: "IX_DropdownItems_ParentId",
                table: "DropdownItems");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "DropdownItems");
        }
    }
}
