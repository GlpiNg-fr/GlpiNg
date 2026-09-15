using GlpiNg.Modules.Abstractions.Items;

namespace GlpiNg.Modules.Abstractions.Notes;

/// <summary>Une note telle qu'un module en a besoin pour l'afficher.</summary>
public sealed record ItemNote(
    int Id,
    string Content,
    string AuthorName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? LastEditorName);

/// <summary>
/// Notes libres rattachées à un objet — l'onglet « Notes » que GLPI pose sur presque toutes ses
/// fiches —, rendues par l'hôte aux modules.
///
/// Les notes sont une entité de l'hôte, comme les documents, et pour la même raison : elles se
/// rattachent à n'importe quel type d'objet, y compris à ceux d'un module que l'hôte ne connaît
/// pas. Ce contrat donne aux modules le strict nécessaire — lire, ajouter, modifier, supprimer —
/// sans leur ouvrir le modèle ni le <c>DbContext</c> concret.
///
/// Le paramètre <c>itemType</c> que portent ces méthodes vient de <see cref="ItemTypes"/>.
///
/// <b>Aucune de ces méthodes ne vérifie que l'appelant a le droit de voir l'objet porteur</b> : la
/// visibilité d'une note suit celle de l'objet, et c'est la page qui l'a déjà établie avant
/// d'afficher l'onglet. Même frontière que pour les pièces jointes.
/// </summary>
public interface IItemNotes
{
    /// <summary>Notes d'un objet, de la plus récente à la plus ancienne.</summary>
    Task<IReadOnlyList<ItemNote>> GetForItemAsync(string itemType, int itemId, CancellationToken cancellationToken = default);

    /// <summary>Nombre de notes d'un objet, pour le compteur d'onglet — sans rapatrier leur contenu.</summary>
    Task<int> CountForItemAsync(string itemType, int itemId, CancellationToken cancellationToken = default);

    /// <summary>Ajoute une note et renvoie son identifiant.</summary>
    Task<int> AddAsync(
        string itemType,
        int itemId,
        string content,
        string authorName,
        int? authorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Réécrit le contenu d'une note. Sans effet si elle n'existe pas — une note supprimée depuis
    /// un autre onglet ne doit pas faire échouer l'enregistrement.
    /// </summary>
    Task UpdateAsync(int noteId, string content, string editorName, CancellationToken cancellationToken = default);

    /// <summary>Supprime une note. Définitif : GlpiNg n'a pas de corbeille pour les notes.</summary>
    Task DeleteAsync(int noteId, CancellationToken cancellationToken = default);
}
