namespace GlpiNg.Modules.Abstractions.Entities;

/// <summary>
/// Marque un objet cloisonné par entité (équivalent des colonnes <c>entities_id</c> /
/// <c>is_recursive</c> de GLPI). Porté par <c>GlpiNg.Modules.Abstractions</c> plutôt que par
/// l'hôte pour que les modules (Inventory, Deployment) puissent l'implémenter sans dépendre de
/// <c>GlpiNg.Web</c>.
///
/// La visibilité effective est calculée par <see cref="EntityScope.Allows"/> et appliquée
/// automatiquement à toutes les requêtes par les filtres globaux du <c>GlpiNgDbContext</c> —
/// aucune page n'a besoin de filtrer elle-même.
/// </summary>
public interface IEntityScoped
{
    /// <summary>
    /// Entité de rattachement. <c>null</c> signifie « pas encore rattaché » : l'objet reste alors
    /// visible de tous, le temps qu'un rattachement soit fait. La migration
    /// <c>AddEntityScoping</c> rattache l'existant à l'entité racine, et
    /// <c>GlpiNgDbContext.SaveChanges</c> renseigne automatiquement l'entité active sur tout
    /// nouvel objet, donc ce cas ne devrait pas se produire en fonctionnement normal.
    /// </summary>
    int? EntityId { get; set; }

    /// <summary>
    /// « Sous-entités visibles » de GLPI : quand c'est vrai, l'objet est aussi visible depuis les
    /// sous-entités de <see cref="EntityId"/>, pas seulement depuis elle.
    /// </summary>
    bool IsRecursive { get; set; }
}
