using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Authentifie un identifiant/mot de passe contre un compte local ou, à défaut, par bind LDAP
/// (voir <see cref="LdapAuthenticationService"/>) — logique partagée entre la connexion cookie
/// (<see cref="Controllers.AccountController"/>) et le grant OAuth2 "password"
/// (<see cref="Controllers.OAuthController"/>), pour que les deux se comportent identiquement
/// vis-à-vis d'un compte local, LDAP existant, ou LDAP à provisionner.
/// </summary>
public class UserCredentialAuthenticator(GlpiNgDbContext db, AppSettingsFileStore settingsStore, LdapAuthenticationService ldapAuth)
{
    private static readonly PasswordHasher<GlpiUser> Hasher = new();

    /// <summary>
    /// Authentifie <paramref name="userName"/>/<paramref name="password"/>. Si
    /// <paramref name="forcedSource"/> vaut 0, seul le mot de passe local est vérifié ; s'il vaut
    /// un AuthLdapServer.Id, seul cet annuaire est essayé ; sinon (null), choix automatique :
    /// mot de passe local si le compte en a un, bind LDAP sinon (annuaire déjà connu pour ce
    /// compte en priorité, sinon tous les annuaires actifs, par défaut d'abord).
    /// </summary>
    public async Task<GlpiUser?> AuthenticateAsync(string userName, string password, int? forcedSource, CancellationToken ct)
    {
        GlpiUser? user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName, ct);

        if (forcedSource == 0)
        {
            return user is not null && user.AuthSource == UserAuthSource.Local && VerifyLocalPassword(user, password)
                ? user
                : null;
        }

        if (forcedSource is { } ldapServerId)
        {
            (bool authenticated, GlpiUser? result) = await AuthenticateLdapAsync(user, userName, password, ldapServerId, ct);
            return authenticated ? result : null;
        }

        if (user is not null && user.AuthSource == UserAuthSource.Local)
        {
            return VerifyLocalPassword(user, password) ? user : null;
        }

        (bool ldapAuthenticated, GlpiUser? ldapUser) = await AuthenticateLdapAsync(user, userName, password, user?.LdapServerId, ct);
        return ldapAuthenticated ? ldapUser : null;
    }

    private static bool VerifyLocalPassword(GlpiUser user, string password) =>
        Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    /// <summary>
    /// Tente le bind LDAP puis, en cas de succès, rattache <paramref name="existingUser"/> à
    /// l'annuaire trouvé (ou provisionne un nouveau compte si AuthSettings.AutoAddUsersFromExternalAuth
    /// est activé et qu'aucun compte local ne correspond).
    /// </summary>
    private async Task<(bool Authenticated, GlpiUser? User)> AuthenticateLdapAsync(
        GlpiUser? existingUser, string userName, string password, int? preferredServerId, CancellationToken ct)
    {
        LdapAuthResult result = await ldapAuth.TryAuthenticateAsync(userName, password, preferredServerId, ct);
        if (!result.Success || result.Server is null)
        {
            return (false, existingUser);
        }

        if (existingUser is not null)
        {
            existingUser.AuthSource = UserAuthSource.Ldap;
            existingUser.LdapServerId = result.Server.Id;
            existingUser.ExternalDn = result.Dn;
            await db.SaveChangesAsync(ct);
            return (true, existingUser);
        }

        AuthSettings authSettings = await settingsStore.ReadSectionAsync<AuthSettings>("AuthSettings", ct);
        if (!authSettings.AutoAddUsersFromExternalAuth)
        {
            return (false, null);
        }

        GlpiUser newUser = new()
        {
            UserName = userName,
            DisplayName = userName,
            // Un compte LDAP ne vérifie jamais PasswordHash (voir GlpiUser.AuthSource) : un hash
            // aléatoire plutôt qu'une chaîne vide, pour rester un hash valide si jamais quelque
            // chose l'appelle malgré tout.
            PasswordHash = string.Empty,
            AuthSource = UserAuthSource.Ldap,
            LdapServerId = result.Server.Id,
            ExternalDn = result.Dn,
        };
        newUser.PasswordHash = Hasher.HashPassword(newUser, Guid.NewGuid().ToString("N"));

        db.Users.Add(newUser);
        await db.SaveChangesAsync(ct);
        return (true, newUser);
    }
}
