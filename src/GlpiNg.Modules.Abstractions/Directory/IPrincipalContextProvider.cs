namespace GlpiNg.Modules.Abstractions.Directory;

/// <summary>
/// Habilitations d'un utilisateur, réduites à ce qu'il faut pour décider s'il est visé par une
/// cible (« cet article est-il pour lui ? »). <see cref="Matches"/> porte la règle une fois pour
/// toutes : sans elle, chaque module qui restreint la visibilité d'un objet réécrirait le même
/// quadruple test, et le premier qui oublierait les groupes ouvrirait un article à tout le monde.
/// </summary>
public sealed class PrincipalContext
{
    public int UserId { get; init; }
    public required string UserName { get; init; }
    public IReadOnlyList<int> EntityIds { get; init; } = [];
    public IReadOnlyList<int> ProfileIds { get; init; } = [];
    public IReadOnlyList<int> GroupIds { get; init; } = [];

    /// <summary>Vrai si cet utilisateur est visé par la cible (<paramref name="kind"/>, <paramref name="itemId"/>).</summary>
    public bool Matches(PrincipalKind kind, int itemId) => kind switch
    {
        PrincipalKind.Entity => EntityIds.Contains(itemId),
        PrincipalKind.Group => GroupIds.Contains(itemId),
        PrincipalKind.Profile => ProfileIds.Contains(itemId),
        PrincipalKind.User => UserId == itemId,
        _ => false,
    };
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
