using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedSuperAdminProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Profiles",
                columns: new[]
                {
                    "Name", "Comment", "IsDefault", "ParcRight", "AssistanceRight", "GestionRight",
                    "OutilsRight", "AdministrationRight", "ConfigurationRight", "CreatedAt", "UpdatedAt"
                },
                values: new object[]
                {
                    "Super-Admin", "Accès complet à toutes les sections (droits non encore appliqués).", true,
                    2, 2, 2, 2, 2, 2,
                    new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Profiles",
                keyColumn: "Name",
                keyValue: "Super-Admin");
        }
    }
}
