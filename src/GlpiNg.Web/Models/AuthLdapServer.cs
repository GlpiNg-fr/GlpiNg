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
/// <summary>Où chercher les groupes d'un utilisateur, repris de « Rechercher dans » du formulaire GLPI.</summary>
public enum LdapGroupSearchType
{
    /// <summary>Dans l'objet utilisateur, via son attribut « memberof ».</summary>
    InUsers,

    /// <summary>Dans les objets groupe, via leur attribut de membres.</summary>
    InGroups,

    /// <summary>Les deux, réunis.</summary>
    Both
}

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

    // --- Onglet "Utilisateurs" : correspondance entre les attributs de l'annuaire et les champs
    // d'un GlpiUser. Appliquée à chaque connexion réussie (voir UserCredentialAuthenticator), donc
    // aussi bien à la création du compte qu'à sa mise à jour. Un champ laissé vide n'est pas lu.
    public string? LastNameField { get; set; } = "sn";
    public string? FirstNameField { get; set; } = "givenname";
    public string? DisplayNameField { get; set; } = "displayname";
    public string? EmailField { get; set; } = "mail";
    public string? PhoneField { get; set; } = "telephonenumber";
    public string? MobileField { get; set; } = "mobile";
    public string? TitleField { get; set; } = "title";
    public string? LocationField { get; set; } = "physicaldeliveryofficename";
    public string? CommentField { get; set; } = "description";

    // --- Onglet "Groupes"
    /// <summary>
    /// Synchronisation des groupes à la connexion. Désactivée par défaut : l'activer change
    /// l'appartenance aux groupes des comptes concernés, ce qui ne doit pas arriver du seul fait
    /// d'une mise à jour.
    /// </summary>
    public bool SynchronizeGroups { get; set; }

    public LdapGroupSearchType GroupSearchType { get; set; } = LdapGroupSearchType.InUsers;

    /// <summary>Attribut de l'utilisateur listant ses groupes, pour <see cref="LdapGroupSearchType.InUsers"/>.</summary>
    public string? GroupMemberOfField { get; set; } = "memberof";

    /// <summary>Filtre de recherche des groupes, pour <see cref="LdapGroupSearchType.InGroups"/>.</summary>
    public string? GroupFilter { get; set; } = "(objectClass=groupOfNames)";

    /// <summary>Attribut portant le nom du groupe.</summary>
    public string? GroupNameField { get; set; } = "cn";

    /// <summary>Attribut du groupe listant ses membres, pour <see cref="LdapGroupSearchType.InGroups"/>.</summary>
    public string? GroupMemberField { get; set; } = "member";

    /// <summary>Vrai si les membres sont désignés par leur DN complet plutôt que par leur identifiant.</summary>
    public bool UseDnForGroupSearch { get; set; } = true;

    // --- Onglet "Informations avancées"
    /// <summary>
    /// STARTTLS sur une connexion en clair, à distinguer de <see cref="UseSsl"/> (LDAPS, chiffré
    /// dès l'ouverture sur un port dédié). Les deux ne se combinent pas.
    /// </summary>
    public bool UseStartTls { get; set; }

    /// <summary>Délai d'attente des opérations LDAP, en secondes.</summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Taille de page des recherches. 0 désactive la pagination — certains annuaires ne la gèrent pas.</summary>
    public int PageSize { get; set; }

    /// <summary>Nombre maximum d'entrées renvoyées par une recherche. 0 = pas de limite côté client.</summary>
    public int MaxResults { get; set; }

    /// <summary>
    /// TAG rapproché de <c>GlpiEntity.AssignmentTag</c> pour rattacher les comptes provisionnés
    /// depuis cet annuaire à une entité — même mécanisme que le TAG des agents d'inventaire.
    /// Vide : les comptes vont dans l'entité racine.
    /// </summary>
    public string? EntityAssignmentTag { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
