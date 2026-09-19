using GlpiNg.Modules.Abstractions.Entities;

namespace GlpiNg.Web.Models.CustomAssets;

/// <summary>
/// Un actif d'un type défini par un administrateur — voir <see cref="CustomAssetDefinition"/>.
///
/// Seuls le nom et le commentaire sont des colonnes : tout le reste est porté par les
/// <see cref="CustomAssetValue"/> du type. Cloisonné par entité comme les actifs livrés, et pour la
/// même raison — un actif appartient à une entité, quel que soit son type.
/// </summary>
public class CustomAsset : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    /// <inheritdoc />
    public bool IsRecursive { get; set; }

    public int Id { get; set; }

    public int CustomAssetDefinitionId { get; set; }
    public CustomAssetDefinition? Definition { get; set; }

    public required string Name { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CustomAssetValue> Values { get; set; } = [];
}

/// <summary>
/// Valeur d'un champ pour un actif. Toujours rangée en texte, y compris pour un nombre ou une
/// date : c'est la contrepartie d'un modèle sans table par type, et la conversion se fait à la
/// saisie et à l'affichage (voir <see cref="CustomAssetValueFormat"/>), en culture invariante pour
/// qu'une base lue depuis une machine en anglais rende les mêmes dates.
/// </summary>
public class CustomAssetValue
{
    public int Id { get; set; }

    public int CustomAssetId { get; set; }
    public CustomAsset? Asset { get; set; }

    public int CustomAssetFieldId { get; set; }
    public CustomAssetField? Field { get; set; }

    public string? Value { get; set; }
}

/// <summary>
/// Modification d'un actif personnalisé, une ligne par champ touché — même forme que les
/// historiques des actifs livrés (<c>PrinterHistoryEntry</c> et consorts), pour que l'onglet
/// « Historique » se lise pareil d'un type à l'autre.
/// </summary>
public class CustomAssetHistoryEntry
{
    public int Id { get; set; }

    public int CustomAssetId { get; set; }
    public CustomAsset? Asset { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string? User { get; set; }

    /// <summary>Libellé du champ modifié, tel qu'il s'affiche sur la fiche.</summary>
    public required string Field { get; set; }

    public string? Description { get; set; }
}
