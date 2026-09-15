namespace GlpiNg.Modules.Abstractions.Items;

/// <summary>
/// Noms des types d'objets, tels que GLPI les écrit dans ses références polymorphes
/// (<c>glpi_documents_items.itemtype</c>, <c>glpi_notepads.itemtype</c>...).
///
/// Une seule liste pour toutes les fonctionnalités qui s'y rattachent — documents, notes, et ce
/// qui viendra. Les tenir séparément par fonctionnalité, c'est répéter les mêmes chaînes
/// littérales autant de fois qu'il y a de fonctionnalités, et laisser une faute de frappe vider
/// un onglet sans que rien ne le signale.
///
/// Constantes plutôt qu'une énumération : la colonne stocke du texte pour rester ouverte aux types
/// qu'un module ajoutera, et une énumération obligerait à recompiler l'hôte pour chacun. Les noms
/// sont ceux de GLPI, et non ceux de GlpiNg, pour que l'import se contente de recopier la colonne.
/// </summary>
public static class ItemTypes
{
    /// <summary>Article de la base de connaissances (<c>KnowbaseItem</c> dans GLPI).</summary>
    public const string KnowledgeBaseArticle = "KnowbaseItem";

    /// <summary>Ordinateur du parc.</summary>
    public const string Computer = "Computer";

    /// <summary>Entité.</summary>
    public const string Entity = "Entity";

    /// <summary>Groupe.</summary>
    public const string Group = "Group";
}
