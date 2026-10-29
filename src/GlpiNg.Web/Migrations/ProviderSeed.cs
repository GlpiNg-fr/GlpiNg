using Microsoft.EntityFrameworkCore.Migrations;

namespace GlpiNg.Web.Migrations;

/// <summary>
/// Données d'installation des jeux de migrations MySQL/PostgreSQL : une migration générée ne
/// contient que le schéma, alors qu'une installation neuve a besoin de l'entité racine, des
/// profils et des clients d'API (sans eux, l'API refuse tout appel). Même contenu que la fin de
/// <c>00000000000000_1.0.0-RC1.cs</c> (SQL Server), qu'on ne modifie pas : elle est peut-être déjà
/// appliquée quelque part. À appeler depuis la première migration de chaque fournisseur.
/// </summary>
internal static class ProviderSeed
{
    private static readonly DateTime Seeded = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

    // 127.0.0.1 en entier, comme GLPI le stocke pour le client d'API « localhost ».
    private const long Localhost = 2130706433L;

    public static void Insert(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.InsertData(
                table: "Entities",
                columns: new[]
                {
                    "Name", "Comment", "TwoFactorAuthRequired", "CustomCssEnabled", "CreatedAt", "UpdatedAt"
                },
                values: new object[]
                {
                    "Root entity", null, false, false, Seeded, Seeded
                });

            migrationBuilder.InsertData(
                table: "Groups",
                columns: new[]
                {
                    "Name", "Comment", "Code", "IsRecursive", "VisibleAsRequester", "VisibleAsObserver",
                    "VisibleAsAssignee", "VisibleAsTask", "CanBeNotified", "CanBeProjectSupervisor",
                    "CanContainItems", "CanContainUsers", "TwoFactorAuthRequired", "CreatedAt", "UpdatedAt"
                },
                values: new object[]
                {
                    "IT Coordinator", "Groupe contenant tous les IT Coordinator.", null, false, true, true,
                    true, true, true, true,
                    false, true, false, Seeded, Seeded
                });

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
                        "Super-Admin", "Accès complet à toutes les sections (droits non encore appliqués).", false,
                        2, 2, 2, 2, 2, 2, Seeded, Seeded
                    },
                    {
                        "Self-Service", "Interface simplifiée : accès en lecture à l'assistance uniquement (droits non encore appliqués).", false,
                        0, 1, 0, 0, 0, 0, Seeded, Seeded
                    },
                    {
                        "Admin", "Accès large à toutes les sections, hors configuration avancée (droits non encore appliqués).", false,
                        2, 2, 2, 2, 2, 1, Seeded, Seeded
                    },
                    {
                        "Technician", "Gère le parc et l'assistance au quotidien (droits non encore appliqués).", true,
                        2, 2, 1, 1, 0, 0, Seeded, Seeded
                    },
                });

            // Clients d'API de GLPI. Les noms restent les siens, en anglais : ce sont ses
            // enregistrements, et un client écrit pour GLPI peut les chercher.
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
                        null, null, 1, null, Seeded, Seeded
                    },
                    {
                        // Désactivé, comme chez GLPI : ouvrir l'API à toutes les adresses est une
                        // décision d'administrateur, pas un défaut d'installation.
                        "full access from anywhere", false, null, null, null,
                        null, null, 1, null, Seeded, Seeded
                    },
                });
    }
}
