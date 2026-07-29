using AnthoDingo.Setup;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace GlpiNg.Web.Services;

/// <summary>
/// Implémentation GlpiNg de <see cref="ISetupInitializer"/>, appelée par le middleware
/// AnthoDingo.Setup pendant l'assistant d'installation (étapes 2 et 3 du wizard).
///
/// Simplification assumée : le schéma est créé via <c>EnsureCreatedAsync</c> plutôt que
/// via des migrations EF Core, car GlpiNg ne maintient pas (encore) trois jeux de
/// migrations distincts — un par provider autorisé (SQL Server / MySQL / PostgreSQL).
/// C'est aussi le choix fait par le projet d'exemple d'AnthoDingo.Setup lui-même.
/// Limite connue : les futures évolutions de schéma ne pourront pas être appliquées via
/// `dotnet ef database update` sur une base déjà installée tant que ce chantier n'est pas
/// fait — il faudra soit générer les 3 jeux de migrations, soit prévoir un mécanisme de
/// migration applicatif dédié.
/// </summary>
public sealed class GlpiNgSetupInitializer : ISetupInitializer
{
    private static readonly PasswordHasher<GlpiUser> Hasher = new();

    public async Task InitializeDatabaseAsync(DbProvider provider, string connectionString, CancellationToken ct = default)
    {
        await using GlpiNgDbContext db = GlpiNgDbContext.Create(provider, connectionString);
        await db.Database.EnsureCreatedAsync(ct);
    }

    public async Task CreateAdminAsync(DbProvider provider, string connectionString, AdminAccount admin, CancellationToken ct = default)
    {
        await using GlpiNgDbContext db = GlpiNgDbContext.Create(provider, connectionString);

        GlpiUser user = new GlpiUser
        {
            UserName = admin.UserName,
            DisplayName = admin.DisplayName,
            IsAdmin = true,
            PasswordHash = string.Empty
        };
        user.PasswordHash = Hasher.HashPassword(user, admin.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }
}
