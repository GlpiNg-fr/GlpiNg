using GlpiNg.Modules.Abstractions.Items;

namespace GlpiNg.Modules.Abstractions.Documents;

/// <summary>
/// Projection d'un document telle qu'un module en a besoin pour l'afficher et le proposer.
///
/// <c>IsContentMissing</c> signale une fiche sans fichier : document repris d'un GLPI dont le
/// dossier <c>files/</c> n'était pas joignable. À signaler à l'écran, et à ne pas proposer au
/// téléchargement.
/// </summary>
public sealed record DocumentSummary(
    int Id,
    string Name,
    string FileName,
    string? MimeType,
    long SizeBytes,
    bool IsContentMissing,
    DateTime CreatedAt,
    string? AuthorName);

/// <summary>
/// Rattachement de fichiers à un objet, rendu par l'hôte aux modules.
///
/// Les documents sont une entité de l'hôte (comme dans GLPI, où un document se rattache à
/// n'importe quel type d'objet), que les modules ne peuvent donc pas manipuler directement : ce
/// contrat leur donne le strict nécessaire — lister, téléverser, rattacher, détacher — sans leur
/// ouvrir le modèle complet ni le stockage sur disque.
///
/// Le paramètre <c>itemType</c> que portent ces méthodes vient de <see cref="ItemTypes"/>.
/// </summary>
public interface IDocumentAttachments
{
    /// <summary>Documents rattachés à un objet, du plus récemment rattaché au plus ancien.</summary>
    Task<IReadOnlyList<DocumentSummary>> GetForItemAsync(string itemType, int itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Téléverse un fichier et le rattache dans la foulée. Renvoie l'identifiant du document —
    /// celui d'un document <b>existant</b> si le contenu est déjà connu, la déduplication se
    /// faisant par empreinte.
    /// </summary>
    Task<int> UploadAndAttachAsync(
        string itemType,
        int itemId,
        string fileName,
        Stream content,
        string? mimeType,
        string? authorName,
        int? authorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Rattache un document déjà présent. Sans effet s'il l'est déjà.</summary>
    Task AttachAsync(string itemType, int itemId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Détache un document sans le supprimer : il peut servir ailleurs, et le retirer d'un article
    /// ne dit rien de son sort dans le reste de l'installation.
    /// </summary>
    Task DetachAsync(string itemType, int itemId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Documents déjà présents correspondant à <paramref name="term"/>, pour en rattacher un sans
    /// le téléverser à nouveau. <paramref name="term"/> vide renvoie les plus récents.
    /// </summary>
    Task<IReadOnlyList<DocumentSummary>> SearchAsync(string? term, int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// URL de téléchargement d'un document. Construite par l'hôte, qui seul connaît la route, pour
    /// qu'un module n'ait pas à la coder en dur.
    /// </summary>
    string DownloadUrl(int documentId);
}
