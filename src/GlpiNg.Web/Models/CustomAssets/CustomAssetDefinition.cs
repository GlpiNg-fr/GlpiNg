using GlpiNg.Modules.Abstractions.Entities;
using GlpiNg.Modules.Inventory.Models;

namespace GlpiNg.Web.Models.CustomAssets;

/// <summary>
/// Type d'actif défini par un administrateur (« Configuration &gt; Actifs personnalisés »,
/// équivalent des actifs génériques de GLPI 11) : un vidéoprojecteur, un véhicule, un badge — tout
/// ce que le parc contient et qu'aucun type livré ne décrit.
///
/// Un type actif apparaît dans le menu Parc avec sa propre liste et ses propres fiches, sans qu'une
/// table ni une page aient été écrites pour lui : les valeurs de ses champs sont rangées dans
/// <see cref="CustomAssetValue"/>, et les écrans sont génériques (voir
/// <c>Components/Pages/CustomAssets</c>).
///
/// Écart assumé avec GLPI, qui engendre une vraie classe PHP par type (<c>Glpi\CustomAsset\…</c>)
/// et donc une table par type : ici tout vit dans deux tables communes. Le parc visé se compte en
/// milliers de lignes, pas en millions, et la contrepartie — pas de colonne typée, donc pas d'index
/// par champ — est sans effet à cette échelle, là où engendrer des tables à chaud demanderait de
/// fabriquer des migrations au moment où un administrateur valide un formulaire.
/// </summary>
public class CustomAssetDefinition
{
    public int Id { get; set; }

    /// <summary>
    /// Nom technique, sans espace ni accent : il sert de segment d'URL (<c>/parc/custom/{nom}</c>)
    /// et de type d'objet pour les documents, les notes et l'historique — voir
    /// <see cref="CustomAssetItemTypes.For(string)"/>. Immuable après création : le changer
    /// détacherait les documents et les notes déjà rattachés, qui portent l'ancien nom.
    /// </summary>
    public required string SystemName { get; set; }

    public required string LabelSingular { get; set; }

    public required string LabelPlural { get; set; }

    /// <summary>Icône Tabler (« ti-tool », « ti-car », ...), affichée dans le menu et sur les fiches.</summary>
    public string Icon { get; set; } = "ti-tool";

    public string? Comment { get; set; }

    /// <summary>
    /// Un type inactif garde ses actifs et sa configuration mais disparaît du menu Parc : c'est le
    /// moyen de retirer un type de la circulation sans détruire ce qu'il contient.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>« Capacités » de GLPI : onglets que la fiche d'un actif de ce type propose.</summary>
    public bool DocumentsEnabled { get; set; } = true;

    /// <inheritdoc cref="DocumentsEnabled"/>
    public bool NotesEnabled { get; set; } = true;

    /// <inheritdoc cref="DocumentsEnabled"/>
    public bool HistoryEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Champs propres au type, en plus du nom et du commentaire que porte tout actif. Un type sans
    /// champ reste utilisable : il décrit alors un parc d'objets qu'on veut seulement compter et
    /// nommer.
    /// </summary>
    public List<CustomAssetField> Fields { get; set; } = [];
}

/// <summary>Nature d'un <see cref="CustomAssetField"/>, qui décide de la saisie et du rendu.</summary>
public enum CustomAssetFieldType
{
    Text,
    LongText,
    Number,
    Boolean,
    Date,
    DateTime,
    Url,

    /// <summary>Liste déroulante puisant dans les intitulés existants (Lieu, Statut, Fabricant...) — voir <see cref="CustomAssetField.DropdownType"/>.</summary>
    Dropdown,
}

/// <summary>
/// Un champ d'un <see cref="CustomAssetDefinition"/>. Sa valeur pour un actif donné est rangée
/// dans <see cref="CustomAssetValue"/>.
/// </summary>
public class CustomAssetField
{
    public int Id { get; set; }

    public int CustomAssetDefinitionId { get; set; }
    public CustomAssetDefinition? Definition { get; set; }

    /// <summary>Libellé affiché à la saisie et en colonne de liste.</summary>
    public required string Label { get; set; }

    public CustomAssetFieldType Type { get; set; } = CustomAssetFieldType.Text;

    /// <summary>
    /// Catégorie d'intitulés proposée quand <see cref="Type"/> vaut
    /// <see cref="CustomAssetFieldType.Dropdown"/>. Réutilise les intitulés déjà tenus par le parc
    /// (Configuration &gt; Intitulés) plutôt que d'ouvrir une deuxième liste de valeurs de
    /// référence à maintenir en parallèle.
    /// </summary>
    public DropdownType? DropdownType { get; set; }

    /// <summary>Un champ obligatoire doit être renseigné pour enregistrer la fiche.</summary>
    public bool IsMandatory { get; set; }

    /// <summary>Ordre d'affichage sur la fiche et en colonnes de liste (les plus petits d'abord).</summary>
    public int SortOrder { get; set; }
}
