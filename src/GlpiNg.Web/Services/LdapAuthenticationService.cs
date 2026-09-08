using System.DirectoryServices.Protocols;
using System.Net;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Resultat d'un bind LDAP. <paramref name="Attributes"/> porte les valeurs des attributs
/// configures dans l'onglet « Utilisateurs » de l'annuaire, indexees par nom d'attribut ;
/// <paramref name="GroupNames"/> les groupes de l'utilisateur quand la synchronisation est
/// activee. Les deux sont vides si rien n'est configure.
/// </summary>
public sealed record LdapAuthResult(
    bool Success,
    AuthLdapServer? Server,
    string? Dn,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<string> GroupNames)
{
    public static readonly LdapAuthResult Failed = new(false, null, null, new Dictionary<string, string>(), []);

    /// <summary>Valeur de l'attribut designe par <paramref name="fieldName"/>, ou null si non configure ou absent.</summary>
    public string? Value(string? fieldName)
        => string.IsNullOrWhiteSpace(fieldName) ? null : Attributes.GetValueOrDefault(fieldName.Trim());
}

public sealed record LdapConnectionTestResult(bool Success, string Message);

/// <summary>
/// Authentifie un identifiant/mot de passe par bind LDAP contre les annuaires configurés
/// (voir <see cref="AuthLdapServer"/>), utilisée par AccountController.Login en complément
/// du mot de passe local. Suit le schéma standard "search-then-bind" de GLPI : un premier
/// bind (compte de service, ou anonyme) sert à retrouver le DN de l'utilisateur via
/// <see cref="AuthLdapServer.LoginField"/>/<see cref="AuthLdapServer.LoginFilter"/>, puis un
/// second bind avec ce DN et le mot de passe saisi valide effectivement les identifiants —
/// GlpiNg ne compare jamais un mot de passe en clair lui-même, il délègue toujours au serveur
/// LDAP.
/// </summary>
public class LdapAuthenticationService(
    IRootDbContextFactory dbFactory,
    AuthSecretProtector protector,
    ILogger<LdapAuthenticationService> logger)
{
    /// <summary>
    /// Tente l'authentification contre les annuaires actifs, par défaut d'abord. Si
    /// <paramref name="preferredServerId"/> est renseigné (source imposée par le sélecteur de
    /// connexion, ou annuaire déjà connu pour ce compte), seul cet annuaire est essayé.
    /// </summary>
    public async Task<LdapAuthResult> TryAuthenticateAsync(string userName, string password, int? preferredServerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return LdapAuthResult.Failed;
        }

        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(ct);
        List<AuthLdapServer> servers = await db.AuthLdapServers.AsNoTracking()
            .Where(s => s.IsActive && (preferredServerId == null || s.Id == preferredServerId))
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        foreach (AuthLdapServer server in servers)
        {
            LdapBindOutcome outcome = TryBind(server, userName, password);
            if (outcome.Dn is not null)
            {
                return new LdapAuthResult(true, server, outcome.Dn, outcome.Attributes, outcome.GroupNames);
            }

            string? failureReason = outcome.FailureReason;

            if (failureReason is not null)
            {
                logger.LogInformation("Bind LDAP échoué sur {Server} pour {User} : {Reason}", server.Name, userName, failureReason);
            }
        }

        return LdapAuthResult.Failed;
    }

    /// <summary>Vérifie qu'un annuaire est joignable et que le bind de service (ou anonyme) fonctionne — sans authentifier d'utilisateur.</summary>
    public LdapConnectionTestResult TestServerConnection(AuthLdapServer server)
    {
        try
        {
            using LdapConnection connection = CreateConnection(server);
            BindService(connection, server);

            SearchRequest searchRequest = new(server.BaseDn, "(objectClass=*)", System.DirectoryServices.Protocols.SearchScope.Base, Array.Empty<string>());
            connection.SendRequest(searchRequest);

            return new LdapConnectionTestResult(true, $"Connexion réussie à {server.Host}:{server.Port} (BaseDN accessible).");
        }
        catch (LdapException ex)
        {
            return new LdapConnectionTestResult(false, $"Échec LDAP : {ex.Message}");
        }
        catch (Exception ex)
        {
            return new LdapConnectionTestResult(false, $"Échec de connexion : {ex.Message}");
        }
    }

    /// <summary>Ce qu'un bind rapporte : le DN si l'authentification a réussi, les attributs demandés, les groupes, ou la raison de l'échec.</summary>
    private sealed record LdapBindOutcome(
        string? Dn,
        IReadOnlyDictionary<string, string> Attributes,
        IReadOnlyList<string> GroupNames,
        string? FailureReason)
    {
        public static LdapBindOutcome Failed(string reason) => new(null, new Dictionary<string, string>(), [], reason);
    }

    private LdapBindOutcome TryBind(AuthLdapServer server, string userName, string password)
    {
        try
        {
            using LdapConnection searchConnection = CreateConnection(server);
            BindService(searchConnection, server);

            string filter = BuildFilter(server, userName);

            // Les attributs de l'onglet « Utilisateurs » sont demandés dès cette recherche : les
            // récupérer par une seconde requête ferait un aller-retour de plus à chaque connexion.
            string[] requested = [.. UserAttributeNames(server).Prepend("dn")];

            SearchRequest searchRequest = new(server.BaseDn, filter, System.DirectoryServices.Protocols.SearchScope.Subtree, requested);
            SearchResponse searchResponse = (SearchResponse)searchConnection.SendRequest(searchRequest);

            if (searchResponse.Entries.Count == 0)
            {
                return LdapBindOutcome.Failed("aucune entrée LDAP correspondante");
            }

            SearchResultEntry entry = searchResponse.Entries[0];
            string userDn = entry.DistinguishedName;

            // Le bind d'authentification utilise le DN trouvé : c'est le serveur LDAP qui valide le
            // mot de passe, jamais GlpiNg.
            using LdapConnection authConnection = CreateConnection(server);
            authConnection.Bind(new NetworkCredential(userDn, password));

            Dictionary<string, string> attributes = ReadAttributes(entry);
            IReadOnlyList<string> groups = server.SynchronizeGroups
                ? ReadGroupNames(searchConnection, server, entry, userDn, userName)
                : [];

            return new LdapBindOutcome(userDn, attributes, groups, null);
        }
        catch (LdapException ex)
        {
            return LdapBindOutcome.Failed(ex.Message);
        }
        catch (Exception ex)
        {
            return LdapBindOutcome.Failed(ex.Message);
        }
    }

    /// <summary>Attributs à demander pour un utilisateur : ceux configurés dans l'onglet « Utilisateurs », plus celui des groupes si nécessaire.</summary>
    private static IEnumerable<string> UserAttributeNames(AuthLdapServer server)
    {
        string?[] candidates =
        [
            server.LastNameField, server.FirstNameField, server.DisplayNameField, server.EmailField,
            server.PhoneField, server.MobileField, server.TitleField, server.LocationField, server.CommentField,
            server.SyncField,
            server.SynchronizeGroups && server.GroupSearchType != LdapGroupSearchType.InGroups ? server.GroupMemberOfField : null,
        ];

        return candidates
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ReadAttributes(SearchResultEntry entry)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        foreach (string name in entry.Attributes.AttributeNames.OfType<string>())
        {
            DirectoryAttribute attribute = entry.Attributes[name];
            if (attribute.Count == 0)
            {
                continue;
            }

            // Première valeur seulement : les champs visés côté GlpiUser sont scalaires.
            if (attribute[0]?.ToString() is { Length: > 0 } value)
            {
                values[name] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// Groupes de l'utilisateur selon le mode choisi dans l'onglet « Groupes » : lus sur l'objet
    /// utilisateur (attribut « memberof »), cherchés parmi les objets groupe (attribut de membres),
    /// ou les deux réunis. Seul le nom est retenu — c'est lui qui sert à retrouver le groupe
    /// correspondant côté GlpiNg.
    /// </summary>
    private List<string> ReadGroupNames(
        LdapConnection connection, AuthLdapServer server, SearchResultEntry entry, string userDn, string userName)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        if (server.GroupSearchType != LdapGroupSearchType.InGroups
            && !string.IsNullOrWhiteSpace(server.GroupMemberOfField)
            && entry.Attributes.Contains(server.GroupMemberOfField))
        {
            foreach (object? value in entry.Attributes[server.GroupMemberOfField])
            {
                if (value?.ToString() is { Length: > 0 } raw && ExtractGroupName(raw, server.GroupNameField) is { } name)
                {
                    names.Add(name);
                }
            }
        }

        if (server.GroupSearchType != LdapGroupSearchType.InUsers && !string.IsNullOrWhiteSpace(server.GroupMemberField))
        {
            try
            {
                string member = EscapeLdapFilterValue(server.UseDnForGroupSearch ? userDn : userName);
                string groupFilter = string.IsNullOrWhiteSpace(server.GroupFilter)
                    ? $"({server.GroupMemberField}={member})"
                    : $"(&{server.GroupFilter}({server.GroupMemberField}={member}))";

                string nameField = string.IsNullOrWhiteSpace(server.GroupNameField) ? "cn" : server.GroupNameField.Trim();
                SearchRequest request = new(server.BaseDn, groupFilter, System.DirectoryServices.Protocols.SearchScope.Subtree, nameField);
                SearchResponse response = (SearchResponse)connection.SendRequest(request);

                foreach (SearchResultEntry group in response.Entries.OfType<SearchResultEntry>())
                {
                    if (group.Attributes.Contains(nameField) && group.Attributes[nameField][0]?.ToString() is { Length: > 0 } name)
                    {
                        names.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                // Un échec de recherche des groupes ne doit pas faire échouer une authentification
                // par ailleurs valide : l'utilisateur entre, sans mise à jour de ses groupes.
                logger.LogInformation("Recherche des groupes LDAP échouée sur {Server} pour {User} : {Reason}", server.Name, userName, ex.Message);
            }
        }

        return [.. names];
    }

    /// <summary>
    /// Nom d'un groupe à partir d'une valeur « memberof », qui est un DN complet
    /// (<c>cn=support,ou=groups,dc=example,dc=com</c>) : on en extrait le composant correspondant à
    /// l'attribut de nom. Une valeur qui n'est pas un DN porte déjà le nom du groupe.
    /// </summary>
    private static string? ExtractGroupName(string raw, string? nameField)
    {
        string prefix = (string.IsNullOrWhiteSpace(nameField) ? "cn" : nameField.Trim()) + "=";

        foreach (string component in raw.Split(','))
        {
            string trimmed = component.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[prefix.Length..];
            }
        }

        return raw.Contains('=') ? null : raw;
    }

    private void BindService(LdapConnection connection, AuthLdapServer server)
    {
        if (server.UseBind && !string.IsNullOrWhiteSpace(server.BindDn))
        {
            string? bindPassword = protector.Unprotect(server.BindPasswordProtected);
            connection.Bind(new NetworkCredential(server.BindDn, bindPassword ?? string.Empty));
        }
        else
        {
            connection.Bind(new NetworkCredential());
        }
    }

    private static LdapConnection CreateConnection(AuthLdapServer server)
    {
        LdapDirectoryIdentifier identifier = new(server.Host, server.Port);
        LdapConnection connection = new(identifier)
        {
            AuthType = AuthType.Basic,
            Timeout = TimeSpan.FromSeconds(server.TimeoutSeconds > 0 ? server.TimeoutSeconds : 5),
        };
        connection.SessionOptions.ProtocolVersion = 3;

        if (server.UseSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }
        else if (server.UseStartTls)
        {
            // STARTTLS eleve une connexion en clair, la ou LDAPS est chiffre des l'ouverture :
            // les deux s'excluent, d'ou le else.
            connection.SessionOptions.StartTransportLayerSecurity(null);
        }

        return connection;
    }

    private static string BuildFilter(AuthLdapServer server, string userName)
    {
        string escaped = EscapeLdapFilterValue(userName);
        string baseFilter = $"({server.LoginField}={escaped})";

        return string.IsNullOrWhiteSpace(server.LoginFilter)
            ? baseFilter
            : $"(&{baseFilter}{server.LoginFilter})";
    }

    // RFC 4515 : échappement des caractères spéciaux d'une valeur de filtre LDAP.
    private static string EscapeLdapFilterValue(string value) => value
        .Replace("\\", "\\5c")
        .Replace("*", "\\2a")
        .Replace("(", "\\28")
        .Replace(")", "\\29")
        .Replace("\0", "\\00");
}
