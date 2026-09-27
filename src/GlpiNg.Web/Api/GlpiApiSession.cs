using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace GlpiNg.Web.Api;

/// <summary>Habilitation telle que l'API la présente : un profil sur une entité (glpi_profiles_users).</summary>
/// <param name="ProfileId">Profil GlpiNg (0 = profil Super-Admin synthétique d'un administrateur sans habilitation).</param>
/// <param name="ProfileName">Nom du profil.</param>
/// <param name="EntityId">Entité GlpiNg.</param>
/// <param name="EntityName">Nom de l'entité.</param>
/// <param name="IsRecursive">Habilitation étendue aux sous-entités.</param>
public sealed record GlpiApiHabilitation(int ProfileId, string ProfileName, int EntityId, string EntityName, bool IsRecursive);

/// <summary>
/// Session d'API : l'équivalent du <c>$_SESSION</c> que GLPI ouvre à <c>initSession</c> — compte,
/// profil actif, entités actives. Partagée par les deux API : la v1 la retrouve par son jeton de
/// session, la v2 en ouvre une temporaire par requête à partir du jeton OAuth.
/// </summary>
public sealed class GlpiApiSession
{
    public required string Token { get; init; }

    public required int UserId { get; init; }

    public required string UserName { get; init; }

    public required string DisplayName { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public bool IsAdmin { get; init; }

    public int? ApiClientId { get; set; }

    public required IReadOnlyList<GlpiApiHabilitation> Habilitations { get; init; }

    /// <summary>Profil actif.</summary>
    public int ActiveProfileId { get; set; }

    /// <summary>Entité active (GlpiNg) ; null = toutes les entités du profil actif (« all »).</summary>
    public int? ActiveEntityId { get; set; }

    /// <summary>« Voir aussi les sous-entités » de l'entité active.</summary>
    public bool ActiveEntityRecursive { get; set; }

    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>Profils distincts de l'utilisateur, dans l'ordre de ses habilitations.</summary>
    public IEnumerable<(int Id, string Name)> Profiles
        => Habilitations.Select(h => (h.ProfileId, h.ProfileName)).Distinct();

    /// <summary>Habilitations du profil actif.</summary>
    public IEnumerable<GlpiApiHabilitation> ActiveProfileHabilitations
        => Habilitations.Where(h => h.ProfileId == ActiveProfileId);
}

/// <summary>
/// Sessions de l'API v1, en mémoire. Un redémarrage les perd : le client reçoit alors
/// ERROR_SESSION_TOKEN_INVALID et rouvre une session, ce qu'il doit savoir faire de toute façon
/// (GLPI expire les siennes).
/// </summary>
public sealed class GlpiApiSessionStore
{
    /// <summary>Durée d'inactivité au-delà de laquelle une session expire.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, GlpiApiSession> _sessions = new(StringComparer.Ordinal);

    public static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20));

    public void Add(GlpiApiSession session)
    {
        Purge();
        _sessions[session.Token] = session;
    }

    public GlpiApiSession? Find(string? token)
    {
        if (string.IsNullOrEmpty(token) || !_sessions.TryGetValue(token.Trim(), out GlpiApiSession? session))
        {
            return null;
        }
        if (DateTime.UtcNow - session.LastActivity > IdleTimeout)
        {
            _sessions.TryRemove(session.Token, out _);
            return null;
        }
        session.LastActivity = DateTime.UtcNow;
        return session;
    }

    public void Remove(string token) => _sessions.TryRemove(token, out _);

    /// <summary>Ferme les sessions d'un compte (désactivation, suppression, jeton régénéré).</summary>
    public void RemoveForUser(int userId)
    {
        foreach (GlpiApiSession session in _sessions.Values.Where(s => s.UserId == userId))
        {
            _sessions.TryRemove(session.Token, out _);
        }
    }

    private void Purge()
    {
        DateTime limit = DateTime.UtcNow - IdleTimeout;
        foreach (GlpiApiSession session in _sessions.Values.Where(s => s.LastActivity < limit))
        {
            _sessions.TryRemove(session.Token, out _);
        }
    }
}
