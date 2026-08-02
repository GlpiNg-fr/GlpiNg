using System.DirectoryServices.Protocols;
using System.Net;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

public sealed record LdapAuthResult(bool Success, AuthLdapServer? Server, string? Dn)
{
    public static readonly LdapAuthResult Failed = new(false, null, null);
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
    IDbContextFactory<GlpiNgDbContext> dbFactory,
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
            string? dn = TryBind(server, userName, password, out string? failureReason);
            if (dn is not null)
            {
                return new LdapAuthResult(true, server, dn);
            }

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

    private string? TryBind(AuthLdapServer server, string userName, string password, out string? failureReason)
    {
        try
        {
            using LdapConnection searchConnection = CreateConnection(server);
            BindService(searchConnection, server);

            string filter = BuildFilter(server, userName);
            SearchRequest searchRequest = new(server.BaseDn, filter, System.DirectoryServices.Protocols.SearchScope.Subtree, "dn");
            SearchResponse searchResponse = (SearchResponse)searchConnection.SendRequest(searchRequest);

            if (searchResponse.Entries.Count == 0)
            {
                failureReason = "aucune entrée LDAP correspondante";
                return null;
            }

            string userDn = searchResponse.Entries[0].DistinguishedName;

            using LdapConnection authConnection = CreateConnection(server);
            authConnection.Bind(new NetworkCredential(userDn, password));

            failureReason = null;
            return userDn;
        }
        catch (LdapException ex)
        {
            failureReason = ex.Message;
            return null;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return null;
        }
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
            Timeout = TimeSpan.FromSeconds(5),
        };
        connection.SessionOptions.ProtocolVersion = 3;
        if (server.UseSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
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
