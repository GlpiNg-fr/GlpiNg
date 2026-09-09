using GlpiNg.Modules.Abstractions.Preferences;

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

    /// <summary>
    /// Nombre de lignes affichées par défaut dans les tableaux paginés (page /preferences).
    /// 0 signifie « rien de choisi » : la valeur par défaut de l'application s'applique alors, ce
    /// qui évite d'avoir à renseigner tous les comptes existants au moment de la migration.
    /// </summary>
    public int ItemsPerPage { get; set; }

    /// <summary>Écriture des adresses MAC à l'affichage — voir MacAddressFormat.</summary>
    public MacAddressFormat MacAddressFormat { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Id de l'utilisateur GLPI d'origine (glpi_users.id) — voir Import/GlpiAdminMySqlImportService, même principe que Computer.SourceGlpiId.</summary>
    public int? SourceGlpiId { get; set; }

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
