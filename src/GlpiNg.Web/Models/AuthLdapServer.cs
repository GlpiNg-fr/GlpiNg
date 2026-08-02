namespace GlpiNg.Web.Models;

/// <summary>
/// Annuaire LDAP externe (glpi_authldaps côté GLPI), utilisé pour authentifier des
/// utilisateurs par bind LDAP (voir Services.LdapAuthenticationService) en plus des
/// comptes locaux de <see cref="GlpiUser"/>. Reprend les champs du formulaire
/// "Annuaire LDAP" de GLPI (front/authldap.form.php) vérifiés sur une instance réelle ;
/// <see cref="UseSsl"/> est un ajout pratique de GlpiNg (le formulaire GLPI de référence
/// ne l'expose pas dans son onglet principal, seulement via le port) nécessaire pour
/// qu'un bind LDAPS fonctionne réellement.
/// </summary>
public class AuthLdapServer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Comment { get; set; }

    public required string Host { get; set; }
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }

    public string? LoginFilter { get; set; }
    public required string BaseDn { get; set; }

    public bool UseBind { get; set; } = true;
    public string? BindDn { get; set; }

    /// <summary>Mot de passe du compte de connexion, chiffré au repos via Services.AuthSecretProtector.</summary>
    public string? BindPasswordProtected { get; set; }

    public string LoginField { get; set; } = "uid";
    public string? SyncField { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
