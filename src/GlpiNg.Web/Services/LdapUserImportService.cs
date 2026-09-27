using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Services;

/// <summary>Un compte d'annuaire tel que proposé à l'import, enrichi de ce que GlpiNg en sait déjà.</summary>
/// <param name="Login">Identifiant, lu depuis l'attribut configuré comme champ d'identifiant.</param>
/// <param name="AlreadyPresent">Vrai si un compte du même identifiant existe déjà : il est alors affiché mais pas sélectionnable.</param>
public sealed record LdapImportCandidate(
    string Login,
    string Dn,
    string? DisplayName,
    string? Email,
    bool AlreadyPresent);

/// <summary>Résumé d'un import manuel depuis un annuaire.</summary>
public sealed record LdapImportResult(int Created, int Skipped, List<string> Warnings);

/// <summary>
/// Import manuel de comptes depuis un annuaire — « Ajout depuis une source externe » de
/// Administration &gt; Utilisateurs, équivalent de <c>front/ldap.import.php</c> côté GLPI.
///
/// Complète le provisionnement automatique à la connexion (voir
/// <see cref="UserCredentialAuthenticator"/>) : celui-ci ne crée un compte qu'au moment où son
/// titulaire se connecte, et seulement si le réglage correspondant est actif. L'import manuel
/// permet de peupler le référentiel à l'avance, et sans activer ce réglage.
///
/// Les comptes créés reçoivent la même préparation que ceux provisionnés à la connexion — mêmes
/// attributs, même habilitation de départ (voir <see cref="LdapAccountProvisioner"/>) — pour qu'un
/// compte importé et un compte provisionné soient indiscernables.
/// </summary>
public sealed class LdapUserImportService(
    GlpiNgDbContext db,
    LdapAuthenticationService ldapAuth,
    LdapAccountProvisioner provisioner)
{
    private static readonly PasswordHasher<GlpiUser> Hasher = new();

    /// <summary>Comptes de l'annuaire correspondant au terme cherché, marqués selon qu'ils existent déjà ou non.</summary>
    public async Task<(IReadOnlyList<LdapImportCandidate> Candidates, string? Error)> SearchAsync(
        int serverId, string? term, CancellationToken ct = default)
    {
        AuthLdapServer? server = await db.AuthLdapServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serverId, ct);
        if (server is null)
        {
            return ([], Tr.T("Annuaire introuvable."));
        }

        LdapSearchOutcome outcome = await ldapAuth.SearchUsersAsync(serverId, term, ct);
        if (!outcome.Success)
        {
            return ([], outcome.Error);
        }

        List<string> logins = [.. outcome.Users.Select(user => user.Login)];
        HashSet<string> existing = [.. await db.Users
            .AsNoTracking()
            .Where(user => logins.Contains(user.UserName))
            .Select(user => user.UserName)
            .ToListAsync(ct)];

        return ([.. outcome.Users.Select(user => new LdapImportCandidate(
            user.Login,
            user.Dn,
            user.Value(server.DisplayNameField) ?? BuildDisplayName(user, server),
            user.Value(server.EmailField),
            existing.Contains(user.Login)))], null);
    }

    /// <summary>
    /// Crée les comptes demandés. Un identifiant déjà présent est ignoré plutôt que mis à jour :
    /// l'écran propose un import, pas une synchronisation, et écraser silencieusement une fiche
    /// existante serait une surprise désagréable.
    /// </summary>
    public async Task<LdapImportResult> ImportAsync(
        int serverId, IReadOnlyCollection<string> logins, CancellationToken ct = default)
    {
        LdapImportResult result = new(0, 0, []);

        AuthLdapServer? server = await db.AuthLdapServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serverId, ct);
        if (server is null)
        {
            return result with { Warnings = ["Annuaire introuvable."] };
        }

        if (logins.Count == 0)
        {
            return result;
        }

        LdapSearchOutcome outcome = await ldapAuth.SearchUsersAsync(serverId, null, ct);
        if (!outcome.Success)
        {
            return result with { Warnings = [outcome.Error ?? Tr.T("Recherche impossible.")] };
        }

        int created = 0;
        int skipped = 0;
        List<string> warnings = [];

        foreach (string login in logins)
        {
            LdapDirectoryUser? directoryUser = outcome.Users.FirstOrDefault(user =>
                string.Equals(user.Login, login, StringComparison.OrdinalIgnoreCase));

            if (directoryUser is null)
            {
                warnings.Add(Tr.T("« {0} » est introuvable dans l'annuaire : ignoré.", login));
                skipped++;
                continue;
            }

            if (await db.Users.AnyAsync(user => user.UserName == login, ct))
            {
                skipped++;
                continue;
            }

            GlpiUser user = new()
            {
                UserName = login,
                DisplayName = login,
                // Un compte d'annuaire ne vérifie jamais son PasswordHash : un hash aléatoire
                // plutôt qu'une chaîne vide, pour rester un hash valide — même choix qu'au
                // provisionnement à la connexion.
                PasswordHash = string.Empty,
                AuthSource = UserAuthSource.Ldap,
                LdapServerId = server.Id,
                ExternalDn = directoryUser.Dn,
            };
            user.PasswordHash = Hasher.HashPassword(user, Guid.NewGuid().ToString("N"));

            provisioner.ApplyAttributes(user, server, directoryUser.Attributes);

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            // Après l'insertion : l'habilitation référence l'utilisateur par son identifiant.
            await provisioner.AssignDefaultHabilitationAsync(user, server, ct);
            await db.SaveChangesAsync(ct);

            created++;
        }

        return new LdapImportResult(created, skipped, warnings);
    }

    private static string? BuildDisplayName(LdapDirectoryUser user, AuthLdapServer server)
    {
        string composed = string.Join(' ', new[]
        {
            user.Value(server.FirstNameField),
            user.Value(server.LastNameField),
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return composed.Length > 0 ? composed : null;
    }
}
