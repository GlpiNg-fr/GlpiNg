namespace GlpiNg.Modules.Abstractions.Entities;

/// <summary>
/// Photographie du cloisonnement applicable à l'utilisateur courant, reprise de la règle de
/// visibilité de GLPI :
///
/// <code>
/// entities_id IN (entités visibles)
/// OU (is_recursive = 1 ET entities_id IN (ancêtres des entités visibles))
/// </code>
///
/// <see cref="VisibleEntityIds"/> = entité active, plus ses sous-entités quand la vue récursive
/// est active. <see cref="AncestorEntityIds"/> = tous les ancêtres de ces entités : un objet
/// marqué « visible dans les sous-entités » (<see cref="IEntityScoped.IsRecursive"/>) et rattaché
/// à une entité parente reste visible depuis l'entité active.
///
/// Immuable et sans dépendance à EF Core : c'est ce que les filtres globaux du
/// <c>GlpiNgDbContext</c> paramètrent à chaque requête. Les collections sont des tableaux (et non
/// des <c>HashSet</c>) parce qu'EF Core ne sait paramétrer que des collections primitives simples
/// dans une clause <c>IN</c>.
/// </summary>
public sealed class EntityScope
{
    /// <summary>Aucun cloisonnement : tout est visible. C'est le défaut hors session utilisateur
    /// (protocole agent, tâches cron, imports, services singleton), et le mode des comptes
    /// administrateurs.</summary>
    public static readonly EntityScope Unrestricted = new(true, null, [], [], false);

    /// <summary>
    /// Cloisonnement fermé : aucune entité visible. C'est ce que reçoit un utilisateur connecté
    /// sans aucune habilitation — il voit des listes vides plutôt que tout le parc. Distinct d'un
    /// <see cref="Restricted"/> sur l'entité racine : sans entité active, rien n'est rattaché
    /// automatiquement aux objets qu'il créerait.
    /// </summary>
    public static readonly EntityScope None = new(false, null, [], [], false);

    private EntityScope(bool isUnrestricted, int? activeEntityId, int[] visible, int[] ancestors, bool includesSubEntities)
    {
        IsUnrestricted = isUnrestricted;
        ActiveEntityId = activeEntityId;
        VisibleEntityIds = visible;
        AncestorEntityIds = ancestors;
        IncludesSubEntities = includesSubEntities;
    }

    /// <summary>Construit un cloisonnement effectif autour d'une entité active.</summary>
    /// <param name="activeEntityId">Entité active choisie par l'utilisateur.</param>
    /// <param name="visibleEntityIds">Entité active + ses sous-entités si la vue récursive est active.</param>
    /// <param name="ancestorEntityIds">Ancêtres des entités visibles.</param>
    /// <param name="includesSubEntities">Vrai si la vue récursive est active.</param>
    public static EntityScope Restricted(int activeEntityId, int[] visibleEntityIds, int[] ancestorEntityIds, bool includesSubEntities)
        => new(false, activeEntityId, visibleEntityIds, ancestorEntityIds, includesSubEntities);

    public bool IsUnrestricted { get; }

    /// <summary>Entité active, ou <c>null</c> quand le cloisonnement ne s'applique pas.</summary>
    public int? ActiveEntityId { get; }

    public int[] VisibleEntityIds { get; }

    public int[] AncestorEntityIds { get; }

    public bool IncludesSubEntities { get; }

    /// <summary>
    /// Pendant en mémoire du filtre global : utile pour vérifier une valeur déjà chargée (contrôle
    /// d'un formulaire, cible d'une affectation) sans repasser par une requête.
    /// </summary>
    public bool Allows(int? entityId, bool isRecursive)
        => IsUnrestricted
           || entityId is null
           || Array.IndexOf(VisibleEntityIds, entityId.Value) >= 0
           || (isRecursive && Array.IndexOf(AncestorEntityIds, entityId.Value) >= 0);
}
