using GlpiNg.Modules.Abstractions.Entities;

namespace GlpiNg.Web.Models.ExternalLinks;

/// <summary>
/// Lien externe associé à un ou plusieurs types d'objets (glpi_links côté GLPI, « Configuration
/// &gt; Liens externes »). Son URL porte des balises <c>[NAME]</c>, <c>[IP]</c>, ... remplacées
/// par les valeurs de l'objet au moment de l'affichage, ce qui permet de pointer un outil tiers
/// — supervision, prise en main, wiki, GMAO — sur le bon élément depuis sa fiche.
///
/// Écarts assumés avec GLPI :
/// <list type="bullet">
/// <item>Les balises forment une liste fermée (voir <see cref="Modules.Abstractions.ExternalLinks.ExternalLinkTags"/>),
/// là où GLPI accepte <c>[FIELD:colonne]</c> sur n'importe quelle colonne de la table.</item>
/// <item>Pas de génération de fichier : GLPI sait faire produire au lien un contenu téléchargeable
/// à partir d'un gabarit, ce qui est un autre métier que pointer une URL.</item>
/// </list>
/// </summary>
public class ExternalLink : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    /// <inheritdoc />
    public bool IsRecursive { get; set; }

    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }

    /// <summary>URL avec ses balises, telle que saisie.</summary>
    public required string Url { get; set; }

    /// <summary>Ouvrir dans un nouvel onglet plutôt que dans celui de la fiche.</summary>
    public bool OpenInNewWindow { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Types d'objets sur les fiches desquels ce lien apparaît. Aucun type associé = le lien
    /// n'apparaît nulle part ; c'est volontairement autorisé, le temps de préparer un lien.
    /// </summary>
    public List<ExternalLinkItemType> ItemTypes { get; set; } = [];
}

/// <summary>
/// Association d'un <see cref="ExternalLink"/> à un type d'objet (glpi_links_itemtypes côté
/// GLPI). Table de liaison plutôt qu'une colonne multivaluée : c'est elle qu'on interroge à
/// chaque ouverture de fiche, et un index sur le type y suffit.
/// </summary>
public class ExternalLinkItemType
{
    public int Id { get; set; }

    public int ExternalLinkId { get; set; }
    public ExternalLink? ExternalLink { get; set; }

    /// <summary>Type d'objet au sens GLPI (« Computer », « Printer », ...).</summary>
    public required string ItemType { get; set; }
}
