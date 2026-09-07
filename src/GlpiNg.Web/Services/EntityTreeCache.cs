using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Garde en mémoire la seule chose dont le calcul du cloisonnement a besoin de l'arbre des
/// entités : le couple (Id, ParentId). Même principe que <see cref="SettingsCacheService"/> —
/// le cloisonnement est évalué à chaque création de contexte EF, donc à chaque opération de
/// chaque page : le relire en base à chaque fois coûterait une requête supplémentaire partout.
///
/// Rafraîchi sur expiration (voir <c>CacheDuration</c>) plutôt que sur invalidation explicite.
/// </summary>
public sealed class EntityTreeCache(IRootDbContextFactory dbFactory)
{
    /// <summary>
    /// Durée de validité du cache. Relire l'arbre à intervalle court plutôt qu'invalider depuis
    /// chaque écriture : les entités se créent et se déplacent depuis une dizaine de points de
    /// sauvegarde de la page Entités, et un oubli sur l'un d'eux donnerait un cloisonnement figé
    /// jusqu'au redémarrage — panne difficile à diagnostiquer. Le coût est d'une requête par
    /// minute pour toute l'application, et une hiérarchie modifiée met au pire ce délai à
    /// s'appliquer. <see cref="Invalidate"/> reste disponible pour rendre un changement immédiat.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();
    private Dictionary<int, int?>? _parentByEntityId;
    private Dictionary<string, int>? _entityIdByAssignmentTag;
    private DateTimeOffset _loadedAt;

    /// <summary>Force la relecture de l'arbre au prochain accès.</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _parentByEntityId = null;
            _entityIdByAssignmentTag = null;
        }
    }

    /// <summary>
    /// Entité <paramref name="entityId"/> et toutes ses sous-entités, à n'importe quelle
    /// profondeur. C'est l'ensemble « visible » quand la vue récursive est active.
    /// </summary>
    public int[] GetSelfAndDescendants(int entityId)
    {
        Dictionary<int, int?> parents = GetParents();

        List<int> result = [entityId];
        // Balayage par niveaux plutôt que récursion : l'arbre est petit, et une hiérarchie
        // accidentellement cyclique (import d'une base GLPI incohérente) ferait boucler une
        // descente naïve — le HashSet des déjà-vus l'en empêche.
        HashSet<int> seen = [entityId];
        Queue<int> pending = new([entityId]);

        while (pending.Count > 0)
        {
            int current = pending.Dequeue();

            foreach ((int id, int? parentId) in parents)
            {
                if (parentId == current && seen.Add(id))
                {
                    result.Add(id);
                    pending.Enqueue(id);
                }
            }
        }

        return [.. result];
    }

    /// <summary>
    /// Ancêtres des entités données (parents, grands-parents...), sans les entités elles-mêmes.
    /// Ce sont les entités depuis lesquelles un objet marqué « visible dans les sous-entités »
    /// reste visible.
    /// </summary>
    public int[] GetAncestors(IEnumerable<int> entityIds)
    {
        Dictionary<int, int?> parents = GetParents();
        HashSet<int> ancestors = [];

        foreach (int entityId in entityIds)
        {
            int? current = parents.GetValueOrDefault(entityId);

            // Garde-fou anti-cycle, même raison que dans GetSelfAndDescendants.
            HashSet<int> walked = [entityId];

            while (current is int parentId && walked.Add(parentId))
            {
                ancestors.Add(parentId);
                current = parents.GetValueOrDefault(parentId);
            }
        }

        return [.. ancestors];
    }

    private Dictionary<int, int?> GetParents()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            if (_parentByEntityId is { } fresh && now - _loadedAt < CacheDuration)
            {
                return fresh;
            }
        }

        // Chargé hors verrou : deux circuits qui arrivent en même temps sur un cache vide feront
        // au pire deux lectures identiques, ce qui est préférable à sérialiser tous les appels
        // derrière une requête base.
        using GlpiNgDbContext db = dbFactory.CreateDbContext();
        var rows = db.Entities
            .AsNoTracking()
            .Select(e => new { e.Id, e.ParentId, e.AssignmentTag })
            .ToList();

        Dictionary<int, int?> loaded = rows.ToDictionary(e => e.Id, e => e.ParentId);

        // Le TAG d'affectation est chargé dans la même passe : c'est la seule autre colonne dont
        // l'import d'inventaire a besoin, et elle change au même rythme que l'arbre.
        // Premier arrivé en cas de doublon : un TAG est censé désigner une entité et une seule,
        // et l'écran Entités ne l'impose pas.
        Dictionary<string, int> byTag = [];
        foreach (var row in rows.Where(e => !string.IsNullOrWhiteSpace(e.AssignmentTag)).OrderBy(e => e.Id))
        {
            byTag.TryAdd(row.AssignmentTag!.Trim(), row.Id);
        }

        lock (_gate)
        {
            _parentByEntityId = loaded;
            _entityIdByAssignmentTag = new Dictionary<string, int>(byTag, StringComparer.OrdinalIgnoreCase);
            _loadedAt = now;
        }

        return loaded;
    }

    /// <summary>
    /// Entité dont le TAG d'affectation (onglet « Informations avancées » de la fiche Entité)
    /// correspond, ou <c>null</c>. C'est ce qui permet à un inventaire d'atterrir directement dans
    /// la bonne entité — équivalent réduit de RuleImportEntity côté GLPI, qui sait en plus jouer
    /// sur le domaine ou le sous-réseau.
    /// </summary>
    public int? GetEntityIdByAssignmentTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        // Force le chargement/rafraîchissement : les deux caches sont peuplés ensemble.
        GetParents();

        Dictionary<string, int>? byTag = _entityIdByAssignmentTag;
        return byTag is not null && byTag.TryGetValue(tag.Trim(), out int entityId) ? entityId : null;
    }

    /// <summary>Entité racine (la première sans parent), utilisée comme entité active par défaut.</summary>
    public int? GetRootEntityId()
    {
        Dictionary<int, int?> parents = GetParents();
        return parents.Where(p => p.Value is null).Select(p => (int?)p.Key).OrderBy(id => id).FirstOrDefault();
    }

    /// <summary>Vrai si <paramref name="candidateId"/> est <paramref name="entityId"/> ou l'une de ses sous-entités.</summary>
    public bool IsSelfOrDescendant(int entityId, int candidateId)
        => Array.IndexOf(GetSelfAndDescendants(entityId), candidateId) >= 0;

    /// <summary>Noms des entités, pour l'affichage du sélecteur.</summary>
    public async Task<Dictionary<int, string>> GetNamesAsync(CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = dbFactory.CreateDbContext();
        return await db.Entities
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);
    }
}
