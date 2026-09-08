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
public class UserCredentialAuthenticator(
    GlpiNgDbContext db,
    SettingsCacheService settingsStore,
    LdapAuthenticationService ldapAuth,
    EntityTreeCache entityTree,
    ILogger<UserCredentialAuthenticator> logger)
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
            logger.LogInformation("Aucun annuaire LDAP n'a authentifie « {User} ». Detail par annuaire ci-dessus ; si rien n'apparait, aucun annuaire actif ne correspondait.", userName);
            return (false, existingUser);
        }

        if (existingUser is not null)
        {
            existingUser.AuthSource = UserAuthSource.Ldap;
            existingUser.LdapServerId = result.Server.Id;
            existingUser.ExternalDn = result.Dn;

            // La correspondance d'attributs est réappliquée à chaque connexion, pas seulement à la
            // création : c'est ce qui fait que l'annuaire reste la source de vérité pour ces
            // champs, sans tâche de synchronisation séparée.
            ApplyLdapAttributes(existingUser, result);
            await SynchronizeGroupsAsync(existingUser, result, ct);

            await db.SaveChangesAsync(ct);
            return (true, existingUser);
        }

        AuthSettings authSettings = await settingsStore.ReadSectionAsync<AuthSettings>("AuthSettings", ct);
        if (!authSettings.AutoAddUsersFromExternalAuth)
        {
            // Le bind LDAP a REUSSI : l'identifiant et le mot de passe sont bons. Le refus vient
            // uniquement de l'absence de compte local et du provisionnement desactive. Sans cette
            // trace, l'ecran de connexion renvoie « Nom d'utilisateur ou mot de passe incorrect »,
            // ce qui designe la mauvaise cause et laisse chercher du cote de l'annuaire.
            logger.LogWarning(
                "Connexion refusee pour « {User} » : le bind sur l'annuaire « {Server} » a pourtant reussi, mais aucun compte local ne correspond et « Ajouter automatiquement les utilisateurs depuis une source externe » est desactive (Configuration > Authentification).",
                userName, result.Server.Name);

            return (false, null);
        }

        logger.LogInformation("Compte « {User} » provisionne depuis l'annuaire « {Server} ».", userName, result.Server.Name);

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

        ApplyLdapAttributes(newUser, result);

        db.Users.Add(newUser);
        await db.SaveChangesAsync(ct);

        // Après le premier SaveChangesAsync : appartenances et habilitation référencent
        // l'utilisateur par son identifiant, qui n'existe qu'une fois la ligne insérée.
        await SynchronizeGroupsAsync(newUser, result, ct);
        await AssignEntityAsync(newUser, result, ct);
        await db.SaveChangesAsync(ct);

        return (true, newUser);
    }

    /// <summary>
    /// Reporte les attributs de l'annuaire sur le compte, selon la correspondance configurée dans
    /// l'onglet « Utilisateurs » de la fiche annuaire. Un attribut non configuré ou absent laisse
    /// le champ inchangé : l'annuaire complète le compte, il ne l'efface pas.
    /// </summary>
    private static void ApplyLdapAttributes(GlpiUser user, LdapAuthResult result)
    {
        AuthLdapServer server = result.Server!;

        user.LastName = result.Value(server.LastNameField) ?? user.LastName;
        user.FirstName = result.Value(server.FirstNameField) ?? user.FirstName;
        user.Email = result.Value(server.EmailField) ?? user.Email;
        user.Phone = result.Value(server.PhoneField) ?? result.Value(server.MobileField) ?? user.Phone;
        user.Location = result.Value(server.LocationField) ?? user.Location;

        // Nom affiché : l'attribut dédié s'il est renseigné, sinon reconstruit depuis prénom et nom
        // — plutôt que de laisser l'identifiant, qui est ce que la création met par défaut.
        string? displayName = result.Value(server.DisplayNameField);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            string composed = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
            displayName = composed.Length > 0 ? composed : null;
        }

        user.DisplayName = displayName ?? user.DisplayName;
    }

    /// <summary>
    /// Donne au compte provisionné une habilitation de départ, sur l'entité dont le TAG
    /// d'affectation correspond à celui de l'annuaire (onglet « Informations avancées »), et sur
    /// l'entité racine à défaut — même mécanisme de TAG que pour les postes inventoriés.
    ///
    /// Le profil retenu est celui marqué par défaut. Sans profil par défaut, aucune habilitation
    /// n'est créée : le compte existe mais ne voit rien, ce qui est le comportement voulu pour un
    /// utilisateur sans droits (voir EntityScope.None) et se corrige depuis la fiche utilisateur.
    /// </summary>
    private async Task AssignEntityAsync(GlpiUser user, LdapAuthResult result, CancellationToken ct)
    {
        int? entityId = entityTree.GetEntityIdByAssignmentTag(result.Server!.EntityAssignmentTag)
                        ?? entityTree.GetRootEntityId();

        if (entityId is not int targetEntityId)
        {
            return;
        }

        GlpiProfile? defaultProfile = await db.Profiles.FirstOrDefaultAsync(profile => profile.IsDefault, ct);
        if (defaultProfile is null)
        {
            return;
        }

        db.UserProfiles.Add(new GlpiUserProfile
        {
            UserId = user.Id,
            EntityId = targetEntityId,
            ProfileId = defaultProfile.Id,
            IsRecursive = false,
        });
    }

    /// <summary>
    /// Aligne les appartenances du compte sur les groupes remontés par l'annuaire, quand la
    /// synchronisation est activée sur la fiche annuaire.
    ///
    /// Rapprochement par nom, et uniquement sur des groupes qui existent déjà dans GlpiNg : un
    /// groupe de l'annuaire sans équivalent local est ignoré plutôt que créé, pour que l'annuaire
    /// ne peuple pas le référentiel des groupes de lui-même.
    ///
    /// Les appartenances retirées ne le sont que parmi les groupes connus de l'annuaire : un
    /// rattachement fait à la main dans GlpiNg, sur un groupe que l'annuaire ne connaît pas, est
    /// conservé.
    /// </summary>
    private async Task SynchronizeGroupsAsync(GlpiUser user, LdapAuthResult result, CancellationToken ct)
    {
        AuthLdapServer server = result.Server!;

        if (!server.SynchronizeGroups || result.GroupNames.Count == 0)
        {
            return;
        }

        List<string> names = [.. result.GroupNames];

        List<GlpiGroup> matched = await db.Groups
            .IgnoreQueryFilters()
            .Where(group => names.Contains(group.Name))
            .ToListAsync(ct);

        List<GlpiGroupUser> current = await db.GroupUsers
            .Where(membership => membership.UserId == user.Id)
            .ToListAsync(ct);

        HashSet<int> targetIds = [.. matched.Select(group => group.Id)];

        foreach (GlpiGroup group in matched.Where(group => current.All(membership => membership.GroupId != group.Id)))
        {
            db.GroupUsers.Add(new GlpiGroupUser { GroupId = group.Id, UserId = user.Id });
        }

        // Retraits limités aux groupes portant un nom connu de l'annuaire : voir la remarque
        // ci-dessus sur les rattachements manuels.
        List<int> knownLocalIds = await db.Groups
            .IgnoreQueryFilters()
            .Where(group => names.Contains(group.Name))
            .Select(group => group.Id)
            .ToListAsync(ct);

        foreach (GlpiGroupUser membership in current.Where(m => knownLocalIds.Contains(m.GroupId) && !targetIds.Contains(m.GroupId)))
        {
            db.GroupUsers.Remove(membership);
        }
    }
}
