namespace GlpiNg.Web.Models;

/// <summary>Source d'authentification d'un <see cref="GlpiUser"/> : mot de passe local, ou bind LDAP contre un <see cref="AuthLdapServer"/>.</summary>
public enum UserAuthSource
{
    Local = 0,
    Ldap = 1,
}

/// <summary>Compte utilisateur GlpiNg, utilisé pour l'authentification applicative et la gestion des utilisateurs (Administration).</summary>
public class GlpiUser
{
    public int Id { get; set; }
    public required string UserName { get; set; }
    public string? DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsAdmin { get; set; }
    public bool SidebarCollapsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Authentification externe (voir Services.LdapAuthenticationService) : un compte créé
    // localement reste AuthSource == Local pour toujours ; un compte provisionné (ou
    // reconnu) via un bind LDAP réussi bascule sur Ldap et ne vérifie plus jamais
    // PasswordHash (voir AccountController.Login) — LdapServerId/ExternalDn ne sont
    // renseignés que dans ce cas.
    public UserAuthSource AuthSource { get; set; } = UserAuthSource.Local;
    public int? LdapServerId { get; set; }
    public AuthLdapServer? LdapServer { get; set; }
    public string? ExternalDn { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;

    // Habilitations (entité + profil, éventuellement récursif) — voir GlpiUserProfile. Un
    // utilisateur peut en avoir plusieurs, comme dans GLPI (glpi_profiles_users).
    public List<GlpiUserProfile> Habilitations { get; set; } = [];

    // Onglet "Sécurité" de GLPI : contrairement à GlpiEntity/GlpiGroup.TwoFactorAuthRequired (qui
    // impose le 2FA), côté utilisateur GLPI expose l'inverse — une dérogation individuelle qui
    // désactive l'obligation autrement héritée de l'entité/du groupe.
    public bool TwoFactorAuthDisabled { get; set; }

    public List<GlpiGroupUser> GroupMemberships { get; set; } = [];
}
