namespace GlpiNg.Modules.Abstractions.Directory;

/// <summary>
/// Habilitations d'un utilisateur, réduites à ce qu'il faut pour décider s'il est visé par une
/// cible (« cet article est-il pour lui ? »). <see cref="Matches(PrincipalKind, int, int?, bool)"/>
/// porte la règle une fois pour toutes : sans elle, chaque module qui restreint la visibilité d'un
/// objet réécrirait le même quadruple test, et le premier qui oublierait les groupes ouvrirait un
/// article à tout le monde.
/// </summary>
public sealed class PrincipalContext
{
    public int UserId { get; init; }
    public required string UserName { get; init; }

    /// <summary>
    /// Entités où l'utilisateur est effectivement habilité, <b>récursivité comprise</b> : une
    /// habilitation récursive sur « Siège » fait figurer ici « Siège » et toutes ses
    /// sous-entités. L'expansion est faite par l'hôte, qui seul connaît l'arbre — un module qui
    /// devrait la refaire n'aurait aucun moyen de le lire.
    /// </summary>
    public IReadOnlyList<int> EntityIds { get; init; } = [];

    /// <summary>
    /// Ancêtres stricts des <see cref="EntityIds"/>. Sert à évaluer une cible <i>récursive</i> :
    /// une cible posée sur « Siège » avec récursivité vise un utilisateur de « Siège &gt; Agence »,
    /// ce qui se teste en regardant si « Siège » est un ancêtre de l'une de ses entités.
    ///
    /// Stocké plutôt que recalculé parce que la règle vit ici, dans un module qui n'a pas accès à
    /// l'arbre des entités.
    /// </summary>
    public IReadOnlyList<int> EntityAncestorIds { get; init; } = [];

    public IReadOnlyList<int> ProfileIds { get; init; } = [];
    public IReadOnlyList<int> GroupIds { get; init; } = [];

    /// <summary>
    /// Vrai si l'utilisateur se trouve dans la portée « entité <paramref name="entityId"/>,
    /// récursive ou non » : soit l'entité est l'une des siennes, soit — si la cible est récursive
    /// — elle est au-dessus de l'une des siennes.
    /// </summary>
    public bool InScope(int entityId, bool recursive)
        => EntityIds.Contains(entityId) || (recursive && EntityAncestorIds.Contains(entityId));

    /// <summary>Vrai si cet utilisateur est visé par la cible (<paramref name="kind"/>, <paramref name="itemId"/>).</summary>
    public bool Matches(PrincipalKind kind, int itemId) => Matches(kind, itemId, null, false);

    /// <summary>
    /// Vrai si cet utilisateur est visé par la cible, en tenant compte de sa portée par entité.
    ///
    /// Reprend les colonnes <c>entities_id</c> / <c>is_recursive</c> que GLPI porte sur ses tables
    /// de visibilité : une cible « profil Technicien » ne vise pas les techniciens de toute
    /// l'installation mais ceux d'une entité donnée, éventuellement de ses sous-entités.
    ///
    /// <paramref name="scopeEntityId"/> ne concerne que les cibles groupe et profil : pour une
    /// cible entité, l'entité <i>est</i> <paramref name="itemId"/>, et pour une cible utilisateur
    /// la portée n'a pas de sens — désigner quelqu'un nommément ne se restreint pas davantage.
    /// </summary>
    public bool Matches(PrincipalKind kind, int itemId, int? scopeEntityId, bool recursive) => kind switch
    {
        PrincipalKind.Entity => InScope(itemId, recursive),
        PrincipalKind.Group => GroupIds.Contains(itemId) && MatchesScope(scopeEntityId, recursive),
        PrincipalKind.Profile => ProfileIds.Contains(itemId) && MatchesScope(scopeEntityId, recursive),
        PrincipalKind.User => UserId == itemId,
        _ => false,
    };

    /// <summary>Une cible sans entité de portée vaut pour toute l'installation — le défaut de GLPI.</summary>
    private bool MatchesScope(int? scopeEntityId, bool recursive)
        => scopeEntityId is not int entityId || InScope(entityId, recursive);
}

/// <summary>
/// Implémenté par l'hôte pour traduire un utilisateur connecté en ses habilitations. Résout dans
/// le sens inverse d'<see cref="IPrincipalDirectory"/> : celle-ci traduit un identifiant de cible
/// en nom affiché pour la configuration, celle-ci traduit un utilisateur en ce qui le vise.
///
/// Généralise <see cref="Deployment.ICurrentUserDeploymentContextProvider"/>, rendu par la même
/// implémentation hôte — voir la remarque sur <see cref="IPrincipalDirectory"/>.
/// </summary>
public interface IPrincipalContextProvider
{
    /// <summary>Habilitations de l'utilisateur, ou <c>null</c> s'il n'existe pas (compte supprimé
    /// dont la session est encore ouverte).</summary>
    Task<PrincipalContext?> GetAsync(int userId, CancellationToken cancellationToken = default);
}
