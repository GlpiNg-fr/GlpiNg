namespace GlpiNg.Web.Models;

/// <summary>
/// Modèle de l'onglet "Autres méthodes d'authentification" de /authentication
/// (auth.others.php côté GLPI : CAS, certificat x509, en-tête HTTP/SSO). Stocké via
/// AppSettingsFileStore (section "AuthOtherSettings"). Configuration/gestion uniquement :
/// aucune de ces méthodes n'est branchée sur le flux de connexion réel (AccountController),
/// contrairement à <see cref="AuthLdapServer"/> — seul le bind LDAP est aujourd'hui une
/// authentification externe fonctionnelle dans GlpiNg.
/// </summary>
public class AuthOtherSettings
{
    // CAS
    public string? CasHost { get; set; }
    public int CasVersion { get; set; } = 2;
    public int CasPort { get; set; } = 443;
    public string? CasBaseDirectory { get; set; }
    public string? CasLogoutUrl { get; set; }

    // Certificat x509
    public string? X509EmailAttribute { get; set; }
    public string? X509RestrictOu { get; set; }
    public string? X509RestrictCn { get; set; }
    public string? X509RestrictO { get; set; }

    // En-tête HTTP / SSO
    public string? HttpAuthField { get; set; }
    public string? SsoLogoutUrl { get; set; }
    public bool StripDomainFromLogin { get; set; }
    public string? HttpAuthLastNameField { get; set; }
    public string? HttpAuthFirstNameField { get; set; }
    public string? HttpAuthCommentField { get; set; }
    public string? HttpAuthEmailField { get; set; }
    public string? HttpAuthPhoneField { get; set; }
}
