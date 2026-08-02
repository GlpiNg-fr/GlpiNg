using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedItCoordinatorGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Groups",
                columns: new[]
                {
                    "Name", "Comment", "Code", "IsRecursive", "VisibleAsRequester", "VisibleAsObserver",
                    "VisibleAsAssignee", "VisibleAsTask", "CanBeNotified", "CanBeProjectSupervisor",
                    "CanContainItems", "CanContainUsers", "CreatedAt", "UpdatedAt"
                },
                values: new object[]
                {
                    "IT Coordinator", "Groupe contenant tous les IT Coordinator.", null, false, true, true,
                    true, true, true, true,
                    false, true, new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Groups",
                keyColumn: "Name",
                keyValue: "IT Coordinator");
        }
    }
}
