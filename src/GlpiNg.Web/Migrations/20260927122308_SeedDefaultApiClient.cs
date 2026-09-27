using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultApiClient : Migration
    {
        /// <summary>127.0.0.1, tel que GLPI le stocke : une adresse IPv4 en entier.</summary>
        private const long Localhost = 2130706433L;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GLPI installe deux clients d'API, et l'API n'accepte un appel que si un client actif
            // correspond à l'adresse de l'appelant : sans eux, elle refuserait tout par
            // ERROR_NOT_ALLOWED_IP, y compris depuis la machine elle-même.
            //
            // Les noms restent ceux de GLPI, en anglais : ce sont ses enregistrements, au même
            // titre que ses noms de tables, et un client écrit pour GLPI peut les chercher.
            migrationBuilder.InsertData(
                table: "ApiClients",
                columns: new[]
                {
                    "Name", "IsActive", "Ipv4RangeStart", "Ipv4RangeEnd", "Ipv6",
                    "AppTokenHash", "AppTokenDate", "LogMethod", "Comment", "CreatedAt", "UpdatedAt"
                },
                values: new object[,]
                {
                    {
                        "full access from localhost", true, Localhost, Localhost, "::1",
                        null, null, 1, null,
                        new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc)
                    },
                    {
                        // Désactivé, comme chez GLPI : ouvrir l'API à toutes les adresses est une
                        // décision d'administrateur, pas un défaut d'installation.
                        "full access from anywhere", false, null, null, null,
                        null, null, 1, null,
                        new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc)
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seuls les deux clients semés repartent : ceux qu'un administrateur a créés restent.
            migrationBuilder.DeleteData(
                table: "ApiClients",
                keyColumn: "Name",
                keyValues: new object[] { "full access from localhost", "full access from anywhere" });
        }
    }
}
