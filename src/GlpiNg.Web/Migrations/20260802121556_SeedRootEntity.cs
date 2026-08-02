using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedRootEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Entities",
                columns: new[] { "Name", "Comment", "CreatedAt", "UpdatedAt" },
                values: new object[]
                {
                    "Root entity", null, new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Entities",
                keyColumn: "Name",
                keyValue: "Root entity");
        }
    }
}
