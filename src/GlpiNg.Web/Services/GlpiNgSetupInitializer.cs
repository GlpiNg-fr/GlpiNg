using AnthoDingo.Setup;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Implémentation GlpiNg de <see cref="ISetupInitializer"/>, appelée par le middleware
/// AnthoDingo.Setup pendant l'assistant d'installation (étapes 2 et 3 du wizard).
///
/// Le schéma SQL Server est créé via <c>MigrateAsync</c> (les migrations EF Core du projet
/// ne sont écrites/testées que pour ce provider — voir les appels
/// SqlServerModelBuilderExtensions dans Migrations/*.Designer.cs) : ça peuple aussi
/// __EFMigrationsHistory, sans quoi MigrationsGateMiddleware considérerait toutes les
/// migrations comme en attente juste après l'installation et redirigerait en boucle vers
/// /update (qui échouerait, les tables existant déjà).
///
/// Pour MySQL/PostgreSQL, à défaut de jeux de migrations dédiés par provider, le schéma est
/// encore créé via <c>EnsureCreatedAsync</c> — limite connue : les futures évolutions de
/// schéma ne pourront pas être appliquées via `dotnet ef database update` sur une base
/// installée avec ces providers tant que ce chantier n'est pas fait.
/// </summary>
public sealed class GlpiNgSetupInitializer : ISetupInitializer
{
    private static readonly PasswordHasher<GlpiUser> Hasher = new();

    public async Task InitializeDatabaseAsync(DbProvider provider, string connectionString, CancellationToken ct = default)
    {
        await using GlpiNgDbContext db = GlpiNgDbContext.Create(provider, connectionString);

        if (provider == DbProvider.SqlServer)
        {
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }
    }

    public async Task CreateAdminAsync(DbProvider provider, string connectionString, AdminAccount admin, CancellationToken ct = default)
    {
        await using GlpiNgDbContext db = GlpiNgDbContext.Create(provider, connectionString);

        // Le profil "Super-Admin" est normalement déjà présent via la migration de seed
        // (SeedSuperAdminProfile), elle-même appliquée par InitializeDatabaseAsync juste avant
        // pour SQL Server (MigrateAsync). Pour MySQL/PostgreSQL, EnsureCreatedAsync ne rejoue pas
        // les migrations de seed (voir la remarque en tête de fichier) : la table existe mais est
        // vide, d'où ce filet de sécurité qui crée le profil s'il manque plutôt que de laisser le
        // premier compte admin sans profil.
        GlpiProfile? superAdminProfile = await db.Profiles.FirstOrDefaultAsync(p => p.Name == "Super-Admin", ct);
        if (superAdminProfile is null)
        {
            superAdminProfile = new GlpiProfile
            {
                Name = "Super-Admin",
                Comment = "Accès complet à toutes les sections (droits non encore appliqués).",
                ParcRight = ProfileRightLevel.Write,
                AssistanceRight = ProfileRightLevel.Write,
                GestionRight = ProfileRightLevel.Write,
                OutilsRight = ProfileRightLevel.Write,
                AdministrationRight = ProfileRightLevel.Write,
                ConfigurationRight = ProfileRightLevel.Write
            };
            db.Profiles.Add(superAdminProfile);
        }

        // Même filet de sécurité pour "Root entity" (normalement déjà présente via
        // SeedRootEntity, appliquée par MigrateAsync pour SQL Server).
        GlpiEntity? rootEntity = await db.Entities.FirstOrDefaultAsync(e => e.Name == "Root entity", ct);
        if (rootEntity is null)
        {
            rootEntity = new GlpiEntity { Name = "Root entity" };
            db.Entities.Add(rootEntity);
        }

        GlpiUser user = new GlpiUser
        {
            UserName = admin.UserName,
            DisplayName = admin.DisplayName,
            IsAdmin = true,
            PasswordHash = string.Empty
        };
        user.PasswordHash = Hasher.HashPassword(user, admin.Password);
        user.Habilitations.Add(new GlpiUserProfile
        {
            User = user,
            Entity = rootEntity,
            Profile = superAdminProfile,
            IsRecursive = true
        });

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
