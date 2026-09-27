using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GlpiNg.Modules.Abstractions.Entities;
using GlpiNg.Modules.Abstractions.Preferences;
using GlpiNg.Web.Api.Glpi;
using GlpiNg.Web.Api.Glpi.Data;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Api;

/// <summary>Résultat du contrôle d'accès d'un appelant (client d'API et jeton d'application).</summary>
/// <param name="ClientId">Client d'API retenu, si l'accès est accordé.</param>
/// <param name="ErrorCode">Code d'erreur GLPI (ERROR_NOT_ALLOWED_IP...), si l'accès est refusé.</param>
/// <param name="ErrorMessage">Message GLPI correspondant.</param>
public sealed record GlpiApiClientCheck(int? ClientId, string? ErrorCode, string? ErrorMessage)
{
    public bool IsAllowed => ErrorCode is null;
}

/// <summary>
/// Contrôle d'accès commun aux deux API : clients autorisés (plage IP, jeton d'application),
/// ouverture d'une session pour un compte, et traduction de cette session en cloisonnement, droits
/// et identité — les mêmes que ceux d'une connexion à l'interface, pour que les filtres globaux et
/// les contrôles d'écriture du reste de l'application s'appliquent tels quels aux appels d'API.
/// </summary>
public sealed class GlpiApiAccess(
    IRootDbContextFactory rootDb,
    EntityTreeCache entityTree,
    GlpiTableCatalog catalog,
    UserCredentialAuthenticator credentials,
    EventLogService eventLog)
{
    /// <summary>Clés de <see cref="HttpContext.Items"/> portant la session d'API et ce qui en découle.</summary>
    public const string SessionItemKey = "glping:api-session";
    public const string ScopeItemKey = "glping:api-scope";
    public const string RightsItemKey = "glping:api-rights";

    /// <summary>Empreinte stockée d'un jeton (d'utilisateur ou d'application).</summary>
    public static string HashToken(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    /// <summary>Nouveau jeton, du même format que ceux de GLPI (40 caractères alphanumériques).</summary>
    public static string NewToken()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(alphabet, 40);
    }

    /// <summary>
    /// Clients d'API correspondant à l'adresse de l'appelant puis jeton d'application — API::initApi
    /// puis API::checkAppToken de GLPI, dans cet ordre et avec les mêmes erreurs.
    /// </summary>
    public async Task<GlpiApiClientCheck> CheckClientAsync(HttpContext http, string? appToken, CancellationToken ct)
    {
        IPAddress? remote = http.Connection.RemoteIpAddress;
        if (remote is { IsIPv4MappedToIPv6: true })
        {
            remote = remote.MapToIPv4();
        }
        string ipText = remote?.ToString() ?? string.Empty;

        await using GlpiNgDbContext db = rootDb.CreateDbContext();
        List<ApiClient> active = await db.ApiClients.AsNoTracking().Where(c => c.IsActive).ToListAsync(ct);

        List<ApiClient> matching;
        if (remote?.AddressFamily == AddressFamily.InterNetwork)
        {
            long ip = Ipv4ToLong(remote);
            matching = [.. active.Where(c => c.Ipv4RangeStart is null || (c.Ipv4RangeStart <= ip && c.Ipv4RangeEnd >= ip))];
        }
        else
        {
            matching = [.. active.Where(c => c.Ipv6 is null || (IPAddress.TryParse(c.Ipv6, out IPAddress? v6) && v6.Equals(remote)))];
        }

        if (matching.Count == 0)
        {
            return new GlpiApiClientCheck(null, "ERROR_NOT_ALLOWED_IP",
                $"There isn't an active API client matching your IP address in the configuration ({ipText})");
        }

        string token = appToken?.Trim() ?? string.Empty;
        ApiClient? client = token.Length > 0
            ? matching.FirstOrDefault(c => c.AppTokenHash == HashToken(token))
            : matching.FirstOrDefault(c => string.IsNullOrEmpty(c.AppTokenHash));

        if (client is null)
        {
            return token.Length > 0
                ? new GlpiApiClientCheck(null, "ERROR_WRONG_APP_TOKEN_PARAMETER", "parameter app_token seems wrong")
                : new GlpiApiClientCheck(null, "ERROR_APP_TOKEN_PARAMETERS_MISSING", "missing parameter app_token");
        }

        return new GlpiApiClientCheck(client.Id, null, null);
    }

    /// <summary>Journalise l'appel selon le réglage du client (API::logEndpointUsage).</summary>
    public async Task LogEndpointAsync(HttpContext http, int? clientId, string endpoint, string? userName, CancellationToken ct)
    {
        if (clientId is not int id)
        {
            return;
        }
        await using GlpiNgDbContext db = rootDb.CreateDbContext();
        ApiClient? client = await db.ApiClients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client?.LogMethod is ApiClientLogMethod.Historical or ApiClientLogMethod.Logs)
        {
            string ip = http.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            string user = userName is null ? string.Empty : $" ({userName})";
            // Texte stocké : il reste en anglais, comme le message que GLPI écrit.
            await eventLog.LogAsync("api", EventLogLevel.Info, $"Enpoint '{endpoint}' called by {ip}{user}",
                "APIClient", client.Id, client.Name, ct);
        }
    }

    public Task<GlpiUser?> AuthenticateAsync(string login, string password, CancellationToken ct)
        => credentials.AuthenticateAsync(login, password, forcedSource: null, ct);

    public async Task<GlpiUser?> FindByUserTokenAsync(string token, CancellationToken ct)
    {
        string hash = HashToken(token);
        await using GlpiNgDbContext db = rootDb.CreateDbContext();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ApiTokenHash == hash && u.IsActive, ct);
    }

    /// <summary>Ouvre une session : habilitations, profil par défaut, toutes les entités du profil actives.</summary>
    public async Task<GlpiApiSession> OpenSessionAsync(GlpiUser user, int? apiClientId, string token, CancellationToken ct)
    {
        await using GlpiNgDbContext db = rootDb.CreateDbContext();
        List<GlpiApiHabilitation> habilitations = await db.UserProfiles.AsNoTracking()
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.Profile.IsDefault).ThenBy(h => h.Id)
            .Select(h => new GlpiApiHabilitation(h.ProfileId, h.Profile.Name, h.EntityId, h.Entity.Name, h.IsRecursive))
            .ToListAsync(ct);

        if (user.IsAdmin && habilitations.Count == 0)
        {
            // Administrateur GlpiNg sans habilitation : GLPI exige un profil actif, on lui présente
            // un Super-Admin sur l'entité racine, récursif — ce qu'il est de fait.
            int root = entityTree.GetRootEntityId() ?? 0;
            string rootName = (await db.Entities.AsNoTracking().Where(e => e.Id == root).Select(e => e.Name).FirstOrDefaultAsync(ct)) ?? "Root entity";
            habilitations.Add(new GlpiApiHabilitation(0, "Super-Admin", root, rootName, true));
        }

        return new GlpiApiSession
        {
            Token = token,
            UserId = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName ?? user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin,
            ApiClientId = apiClientId,
            Habilitations = habilitations,
            ActiveProfileId = habilitations.Count > 0 ? habilitations[0].ProfileId : 0,
        };
    }

    /// <summary>Entités actives de la session (identifiants GlpiNg).</summary>
    public int[] ActiveEntityIds(GlpiApiSession session)
    {
        if (session.ActiveEntityId is int active)
        {
            return session.ActiveEntityRecursive ? entityTree.GetSelfAndDescendants(active) : [active];
        }
        return [.. session.ActiveProfileHabilitations
            .SelectMany(h => h.IsRecursive ? entityTree.GetSelfAndDescendants(h.EntityId) : [h.EntityId])
            .Distinct()];
    }

    /// <summary>Cloisonnement de la session : entités actives, ou tout pour un administrateur sur « toutes ».</summary>
    public EntityScope ScopeFor(GlpiApiSession session)
    {
        if (session.IsAdmin && session.ActiveEntityId is null)
        {
            return EntityScope.Unrestricted;
        }

        int[] visible = ActiveEntityIds(session);
        if (visible.Length == 0)
        {
            return EntityScope.None;
        }

        int activeEntity = session.ActiveEntityId ?? session.ActiveProfileHabilitations.Select(h => h.EntityId).FirstOrDefault(visible[0]);
        bool recursive = session.ActiveEntityId is null || session.ActiveEntityRecursive;
        return EntityScope.Restricted(activeEntity, visible, entityTree.GetAncestors(visible), recursive);
    }

    /// <summary>Droits du profil actif (tous pour un administrateur, comme dans l'interface).</summary>
    public async Task<ProfileRights> RightsForAsync(GlpiApiSession session, CancellationToken ct)
    {
        if (session.IsAdmin)
        {
            return ProfileRights.Full;
        }
        await using GlpiNgDbContext db = rootDb.CreateDbContext();
        GlpiProfile? profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == session.ActiveProfileId, ct);
        if (profile is null)
        {
            return ProfileRights.None;
        }
        return new ProfileRights(section => GlpiApiProfiles.LevelOf(profile, section));
    }

    /// <summary>
    /// Rattache la session à la requête : cloisonnement et droits (lus par EntityScopeProvider et
    /// ProfileRightsProvider, donc par tout contexte EF créé ensuite), et identité — avec le marqueur
    /// de session applicative, pour qu'aucun service ne la prenne pour un appel machine non cloisonné.
    /// </summary>
    public async Task AttachAsync(HttpContext http, GlpiApiSession session, CancellationToken ct)
    {
        EntityScope scope = ScopeFor(session);
        ProfileRights rights = await RightsForAsync(session, ct);

        http.Items[SessionItemKey] = session;
        http.Items[ScopeItemKey] = scope;
        http.Items[RightsItemKey] = rights;

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Name, session.DisplayName),
            new(UserEntityAccessService.UnrestrictedClaim, scope.IsUnrestricted ? "1" : "0"),
            new(UserEntityAccessService.RecursiveClaim, scope.IncludesSubEntities ? "1" : "0"),
        ];
        if (scope.ActiveEntityId is int active)
        {
            claims.Add(new Claim(UserEntityAccessService.ActiveEntityClaim, active.ToString()));
        }
        claims.AddRange(Enum.GetValues<ProfileSection>()
            .Select(section => new Claim(ProfileRightsService.ClaimTypeFor(section), ((int)rights.Level(section)).ToString())));

        http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "GlpiApi"));
    }

    /// <summary>Contexte d'accès aux tables GLPI virtuelles pour la requête (après <see cref="AttachAsync"/>).</summary>
    public async Task<GlpiDataContext> CreateDataContextAsync(HttpContext http, GlpiApiSession session, CancellationToken ct)
    {
        GlpiNgDbContext db = await http.RequestServices.GetRequiredService<IDbContextFactory<GlpiNgDbContext>>().CreateDbContextAsync(ct);
        http.Response.RegisterForDisposeAsync(db);
        UserPreferenceValues preferences = await http.RequestServices.GetRequiredService<IUserPreferences>().GetAsync(ct);
        return new GlpiDataContext(db, catalog, entityTree, http.RequestServices,
            session.UserId, session.UserName, session.DisplayName, preferences.TimeZone);
    }

    private static long Ipv4ToLong(IPAddress ip)
    {
        byte[] b = ip.GetAddressBytes();
        return ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
    }
}
