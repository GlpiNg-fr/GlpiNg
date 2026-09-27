using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Abstractions.Entities;

namespace GlpiNg.Web.Models.Documents;

/// <summary>
/// Un document, équivalent de <c>glpi_documents</c> : un fichier téléversé une fois, rattachable
/// ensuite à autant d'objets qu'on veut (voir <see cref="DocumentItem"/>).
///
/// Entité de l'hôte et non d'un module, comme dans GLPI : un document n'appartient pas à la base
/// de connaissances plus qu'au parc ou aux tickets à venir. C'est aussi ce qui permet au même
/// fichier d'illustrer une procédure et de documenter un matériel sans être téléversé deux fois —
/// la déduplication par empreinte (voir <see cref="Sha256"/>) rend d'ailleurs cette économie
/// automatique.
///
/// Le contenu ne vit pas en base : <see cref="StoragePath"/> désigne un fichier sous la racine de
/// stockage. Une base de données est un mauvais entrepôt de fichiers — sauvegardes alourdies,
/// mémoire consommée à chaque lecture — et le reste de GlpiNg range déjà ses fichiers ainsi (voir
/// les paquets de déploiement).
/// </summary>
public class Document : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    /// <inheritdoc />
    public bool IsRecursive { get; set; }

    public int Id { get; set; }

    /// <summary>Nom affiché (« Nom » dans GLPI), libre et distinct du nom de fichier.</summary>
    public required string Name { get; set; }

    /// <summary>Nom du fichier d'origine, conservé pour le renvoyer au téléchargement.</summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Type MIME déclaré. Jamais utilisé pour décider d'un rendu dans le navigateur : le
    /// téléchargement force le téléchargement (voir DocumentsController), sans quoi un fichier
    /// HTML téléversé s'exécuterait dans l'origine de l'application.
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Empreinte SHA-256 du contenu, en minuscules. Sert de clé de rangement sur disque et de
    /// déduplication : deux documents identiques partagent le même fichier.
    ///
    /// GLPI utilise SHA-1, conservé à part dans <see cref="SourceSha1"/> : le recalculer en
    /// SHA-256 à l'import coûte une lecture du fichier et évite d'installer durablement une
    /// empreinte dont la résistance aux collisions n'est plus défendable.
    /// </summary>
    public required string Sha256 { get; set; }

    /// <summary>Chemin du fichier sous la racine de stockage, relatif — voir DocumentStorageService.</summary>
    public required string StoragePath { get; set; }

    public long SizeBytes { get; set; }

    public int? CategoryId { get; set; }
    public DocumentCategory? Category { get; set; }

    public string? Comment { get; set; }

    /// <summary>
    /// Lien web associé (<c>glpi_documents.link</c>) : dans GLPI, un « document » peut n'être
    /// qu'une URL. Repris pour ne pas perdre l'information à l'import.
    /// </summary>
    public string? Link { get; set; }

    /// <summary>Compte ayant téléversé le document, s'il existe encore ; le nom est conservé à part.</summary>
    public int? AuthorUserId { get; set; }

    /// <summary>Nom du téléverseur — dupliqué pour survivre à la suppression du compte.</summary>
    public string? AuthorName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Identifiant GLPI d'origine (<c>glpi_documents.id</c>) — rend l'import idempotent.</summary>
    public int? SourceGlpiId { get; set; }

    /// <summary>
    /// Empreinte SHA-1 telle que GLPI la stockait. Conservée sans être utilisée pour la
    /// déduplication : elle sert à reconnaître un document déjà repris quand le fichier lui-même
    /// n'a pas pu être récupéré, et donc quand <see cref="Sha256"/> n'a pas pu être calculé.
    /// </summary>
    public string? SourceSha1 { get; set; }

    /// <summary>
    /// Vrai quand la fiche existe sans son fichier : import dont le dossier <c>files/</c> de GLPI
    /// n'était pas joignable. Le document reste visible et rattaché — perdre le lien vaudrait pire
    /// que perdre l'octet — mais il n'est pas téléchargeable, et l'écran le dit.
    /// </summary>
    public bool IsContentMissing { get; set; }

    public List<DocumentItem> Items { get; set; } = [];
}

/// <summary>
/// Catégorie de documents, équivalent de <c>glpi_documentcategories</c> : une arborescence, comme
/// les catégories de la base de connaissances.
/// </summary>
public class DocumentCategory : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    /// <inheritdoc />
    public bool IsRecursive { get; set; }

    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }

    public int? ParentId { get; set; }
    public DocumentCategory? Parent { get; set; }
    public List<DocumentCategory> Children { get; set; } = [];

    public List<Document> Documents { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Identifiant GLPI d'origine (<c>glpi_documentcategories.id</c>).</summary>
    public int? SourceGlpiId { get; set; }
}

/// <summary>
/// Rattachement d'un document à un objet, équivalent de <c>glpi_documents_items</c>.
///
/// Référence polymorphe non contrainte (<see cref="ItemType"/> + <see cref="ItemId"/>, sans clé
/// étrangère), comme GLPI et comme les cibles de visibilité de la base de connaissances : les
/// objets visés vivent dans des modules que l'hôte ne peut pas tous connaître à l'avance, et une
/// FK par type obligerait à modifier cette table chaque fois qu'un module nouveau veut attacher
/// un fichier.
///
/// <see cref="ItemType"/> reprend les noms de GLPI (<c>KnowbaseItem</c>, <c>Computer</c>...) pour
/// que l'import se contente de recopier la colonne — voir <see cref="ItemTypes"/>.
/// </summary>
public class DocumentItem
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public required string ItemType { get; set; }
    public int ItemId { get; set; }

    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
}
