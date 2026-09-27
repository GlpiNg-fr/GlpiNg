using GlpiNg.Modules.Abstractions.Entities;
using GlpiNg.Web.Data;
using GlpiNg.Web.Services;

namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>Correspondance nom ↔ identifiant d'une table de référence (intitulés, utilisateurs...).</summary>
public sealed class GlpiLookup
{
    private readonly Dictionary<int, string> _nameById;
    private readonly Dictionary<string, int> _idByName;

    public GlpiLookup(IEnumerable<KeyValuePair<int, string?>> rows)
    {
        _nameById = [];
        _idByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach ((int id, string? name) in rows.OrderBy(r => r.Key))
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            _nameById[id] = name;
            _idByName.TryAdd(name.Trim(), id);
        }
    }

    /// <summary>Identifiant de la valeur portant ce nom, 0 (« aucune » pour GLPI) sinon.</summary>
    public int IdOf(string? name) => string.IsNullOrWhiteSpace(name) ? 0 : _idByName.GetValueOrDefault(name.Trim());

    public string? NameOf(int id) => _nameById.GetValueOrDefault(id);
}

/// <summary>
/// Contexte d'une requête d'API sur les tables GLPI virtuelles : contexte EF (cloisonné et soumis
/// aux droits de la session), catalogue des tables, utilisateur, et caches de la requête.
/// </summary>
public sealed class GlpiDataContext(
    GlpiNgDbContext db,
    GlpiTableCatalog catalog,
    EntityTreeCache entityTree,
    IServiceProvider services,
    int? userId,
    string? userName,
    string? userDisplayName,
    TimeZoneInfo timeZone)
{
    /// <summary>Services de la requête, pour réutiliser la logique métier des modules (échéances SLA...).</summary>
    public IServiceProvider Services { get; } = services;

    /// <summary>Nom affiché de l'utilisateur, celui que GlpiNg fige sur les suivis et tâches qu'il écrit.</summary>
    public string? UserDisplayName { get; } = userDisplayName;

    private readonly Dictionary<string, GlpiLookup> _lookups = new(StringComparer.Ordinal);
    private int? _rootEntityId;

    public GlpiNgDbContext Db { get; } = db;

    public GlpiTableCatalog Catalog { get; } = catalog;

    public EntityTreeCache EntityTree { get; } = entityTree;

    public EntityScope Scope => Db.EntityScope;

    public ProfileRights Rights => Db.ProfileRights;

    public int? UserId { get; } = userId;

    public string? UserName { get; } = userName;

    /// <summary>Fuseau dans lequel les dates sont échangées (celui de l'utilisateur).</summary>
    public TimeZoneInfo TimeZone { get; } = timeZone;

    public IGlpiTable Table(string name) => Catalog.Get(name);

    /// <summary>Identifiant GlpiNg de l'entité racine, que GLPI numérote 0.</summary>
    public int RootEntityId => _rootEntityId ??= EntityTree.GetRootEntityId() ?? 0;

    /// <summary>Identifiant GLPI d'une entité : la racine (ou « aucune ») devient 0.</summary>
    public int ToApiEntity(int? entityId) => entityId is null || entityId == RootEntityId ? 0 : entityId.Value;

    /// <summary>Identifiant GlpiNg d'une entité désignée par son identifiant GLPI.</summary>
    public int FromApiEntity(long apiId) => apiId == 0 ? RootEntityId : (int)apiId;

    /// <summary>Entité active de la session, en identifiant GLPI.</summary>
    public int ActiveApiEntity => ToApiEntity(Scope.ActiveEntityId ?? RootEntityId);

    public DateTime ToUserTime(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    public DateTime FromUserTime(DateTime local)
        => local.Kind == DateTimeKind.Utc
            ? local
            : TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);

    /// <summary>Table de correspondance nom ↔ identifiant, chargée une fois par requête.</summary>
    public async Task<GlpiLookup> LookupAsync(string table, string nameColumn, CancellationToken ct)
    {
        string key = table + "." + nameColumn;
        if (!_lookups.TryGetValue(key, out GlpiLookup? lookup))
        {
            Dictionary<int, object?> values = await Table(table).GetColumnAsync(this, nameColumn, null, ct);
            lookup = new GlpiLookup(values.Select(v => new KeyValuePair<int, string?>(v.Key, GlpiValue.ToText(v.Value))));
            _lookups[key] = lookup;
        }
        return lookup;
    }

    /// <summary>Table de correspondance déjà chargée par <see cref="LookupAsync"/>.</summary>
    public GlpiLookup Lookup(string table, string nameColumn)
        => _lookups.TryGetValue(table + "." + nameColumn, out GlpiLookup? lookup)
            ? lookup
            : throw new InvalidOperationException($"Correspondance {table}.{nameColumn} non préchargée.");

    /// <summary>Mémoire de requête pour les valeurs calculées (noms complets d'arborescences...).</summary>
    public Dictionary<string, object> Cache { get; } = new(StringComparer.Ordinal);
}
