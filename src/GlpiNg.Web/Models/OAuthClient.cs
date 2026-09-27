namespace GlpiNg.Web.Models;

/// <summary>
/// Types de grant OAuth2 qu'un <see cref="OAuthClient"/> peut demander (glpi_oauth_clients.grants
/// côté GLPI). Seuls <see cref="ClientCredentials"/> et <see cref="Password"/> sont aujourd'hui
/// fonctionnels dans GlpiNg (voir Controllers.OAuthController) : <see cref="AuthorizationCode"/>
/// exigerait un écran de consentement, un stockage de code d'autorisation et PKCE — conservé ici
/// uniquement pour la fidélité du formulaire de gestion avec GLPI.
/// </summary>
[Flags]
public enum OAuthGrantTypes
{
    None = 0,
    AuthorizationCode = 1 << 0,
    Password = 1 << 1,
    ClientCredentials = 1 << 2,
}

/// <summary>Scopes OAuth2 qu'un <see cref="OAuthClient"/> peut se voir accorder, repris de GLPI (front/oauthclient.form.php).</summary>
[Flags]
public enum OAuthScopes
{
    None = 0,
    Email = 1 << 0,
    User = 1 << 1,
    Api = 1 << 2,
    Inventory = 1 << 3,
    Status = 1 << 4,
    GraphQl = 1 << 5,
}

/// <summary>
/// Client OAuth2 externe (glpi_oauth_clients côté GLPI) autorisé à obtenir un jeton d'accès
/// auprès de /oauth2/token (voir Controllers.OAuthController) pour appeler l'API GlpiNg — par
/// exemple /admin/import/glpi, protégé par la policy "OAuthApiAccess".
/// </summary>
public class OAuthClient
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public required string ClientId { get; set; }

    /// <summary>Hash (jamais le secret en clair) — contrairement à GLPI, GlpiNg ne permet pas de "révéler" un secret existant, seulement de le régénérer.</summary>
    public required string ClientSecretHash { get; set; }

    public OAuthGrantTypes Grants { get; set; } = OAuthGrantTypes.ClientCredentials;
    public OAuthScopes Scopes { get; set; } = OAuthScopes.Api;
    public bool IsActive { get; set; } = true;
    public string? Comment { get; set; }

    // Une valeur par ligne — pas de table enfant dédiée, ce sont de simples listes de chaînes
    // sans métadonnées propres (contrairement à AuthLdapServer par exemple).
    public string? RedirectUrisRaw { get; set; }
    public string? IpRestrictionsRaw { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
