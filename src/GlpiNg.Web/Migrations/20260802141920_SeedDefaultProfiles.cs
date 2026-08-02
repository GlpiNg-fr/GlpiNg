using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GLPI installe 4 profils par défaut (Self-Service, Admin, Super-Admin, Technician),
            // vérifié sur une instance GLPI réelle (front/profile.php) — et c'est "Technician" qui
            // y est marqué "profil par défaut", pas "Super-Admin" comme semé par erreur dans
            // SeedSuperAdminProfile.
            migrationBuilder.UpdateData(
                table: "Profiles",
                keyColumn: "Name",
                keyValue: "Super-Admin",
                column: "IsDefault",
                value: false);

            migrationBuilder.InsertData(
                table: "Profiles",
                columns: new[]
                {
                    "Name", "Comment", "IsDefault", "ParcRight", "AssistanceRight", "GestionRight",
                    "OutilsRight", "AdministrationRight", "ConfigurationRight", "CreatedAt", "UpdatedAt"
                },
                values: new object[,]
                {
                    {
                        "Self-Service", "Interface simplifiée : accès en lecture à l'assistance uniquement (droits non encore appliqués).", false,
                        0, 1, 0, 0, 0, 0,
                        new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        "Admin", "Accès large à toutes les sections, hors configuration avancée (droits non encore appliqués).", false,
                        2, 2, 2, 2, 2, 1,
                        new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        "Technician", "Gère le parc et l'assistance au quotidien (droits non encore appliqués).", true,
                        2, 2, 1, 1, 0, 0,
                        new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc)
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Profiles",
                keyColumn: "Name",
                keyValues: new object[] { "Self-Service", "Admin", "Technician" });

            migrationBuilder.UpdateData(
                table: "Profiles",
                keyColumn: "Name",
                keyValue: "Super-Admin",
                column: "IsDefault",
                value: true);
        }
    }
}
