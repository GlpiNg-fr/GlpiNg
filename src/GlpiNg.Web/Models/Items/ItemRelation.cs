namespace GlpiNg.Web.Models.Items;

/// <summary>
/// Rattachement polymorphe d'un objet (<see cref="ItemType"/> + <see cref="ItemId"/>) à un autre
/// (<see cref="TargetType"/> + <see cref="TargetId"/>) pour une nature donnée (<see cref="Relation"/>,
/// voir <c>ItemRelationKinds</c>). Remplace une table de liaison par couple de types, comme
/// <c>glpi_certificates_items</c>, <c>glpi_items_tickets</c>...
///
/// Aucune clé étrangère : les deux extrémités vivent dans des modules différents. Un lien vers un
/// objet supprimé est simplement ignoré à la lecture.
/// </summary>
public class ItemRelation
{
    public int Id { get; set; }

    public required string ItemType { get; set; }

    public int ItemId { get; set; }

    public required string Relation { get; set; }

    public required string TargetType { get; set; }

    public int TargetId { get; set; }
}

/// <summary>Informations de gestion d'un objet — voir <c>IItemManagement</c>. Une ligne par objet.</summary>
public class ItemManagement
{
    public int Id { get; set; }

    public required string ItemType { get; set; }

    public int ItemId { get; set; }

    public int? SupplierId { get; set; }

    public int? BudgetId { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public DateTime? UseDate { get; set; }

    public int? WarrantyMonths { get; set; }

    public string? WarrantyInfo { get; set; }

    /// <summary>Valeur d'achat. <c>decimal</c> : de la monnaie, pas une mesure.</summary>
    public decimal? Value { get; set; }

    public string? OrderNumber { get; set; }

    public string? DeliveryNumber { get; set; }
}

/// <summary>Coût imputé à un objet — voir <c>IItemCosts</c>. Polymorphe, sans clé étrangère, comme les notes.</summary>
public class ItemCost
{
    public int Id { get; set; }

    public required string ItemType { get; set; }

    public int ItemId { get; set; }

    public required string Name { get; set; }

    /// <summary>Date du coût, valeur murale saisie à la main.</summary>
    public DateTime? Date { get; set; }

    public decimal Amount { get; set; }

    public string? Comment { get; set; }
}
