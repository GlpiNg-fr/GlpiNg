using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AllowSameDropdownNameUnderDifferentParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DropdownItems_Type_Name",
                table: "DropdownItems");

            migrationBuilder.CreateIndex(
                name: "IX_DropdownItems_Type_EntityId_ParentId_Name",
                table: "DropdownItems",
                columns: new[] { "Type", "EntityId", "ParentId", "Name" },
                unique: true,
                filter: "[EntityId] IS NOT NULL AND [ParentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DropdownItems_Type_EntityId_ParentId_Name",
                table: "DropdownItems");

            migrationBuilder.CreateIndex(
                name: "IX_DropdownItems_Type_Name",
                table: "DropdownItems",
                columns: new[] { "Type", "Name" },
                unique: true);
        }
    }
}
