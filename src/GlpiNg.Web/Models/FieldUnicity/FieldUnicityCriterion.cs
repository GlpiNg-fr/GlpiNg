using GlpiNg.Modules.Abstractions.Entities;

namespace GlpiNg.Web.Models.FieldUnicity;

/// <summary>
/// Critère d'unicité des champs (<c>glpi_fieldunicities</c> côté GLPI, « Configuration &gt; Unicité
/// des champs ») : pour un type d'objet donné, la combinaison des champs listés ne doit exister
/// qu'une fois. Un critère qui correspond peut refuser la création, la signaler par notification,
/// ou les deux — voir <see cref="Services.FieldUnicity.FieldUnicityService"/>, qui l'applique.
///
/// Ce sont bien les champs *ensemble* qui doivent être uniques, et non chacun de leur côté : deux
/// champs dans un même critère décrivent « pas deux objets avec ces deux valeurs-là », là où deux
/// critères d'un champ chacun décrivent « pas deux objets de même nom, et pas deux objets de même
/// numéro de série ». C'est la règle de GLPI, et l'écran le rappelle.
///
/// Écarts assumés avec GLPI :
/// <list type="bullet">
/// <item>Le contrôle porte sur la création (import d'inventaire et saisie manuelle), pas sur toutes
/// les modifications ultérieures : GLPI passe par <c>CommonDBTM</c>, que GlpiNg n'a pas.</item>
/// <item>Seuls les champs texte sont proposés — voir <see cref="Services.FieldUnicity.FieldUnicityCatalog"/>.</item>
/// </list>
/// </summary>
public class FieldUnicityCriterion : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    /// <inheritdoc />
    public bool IsRecursive { get; set; }

    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Comment { get; set; }

    /// <summary>Type d'objet au sens GLPI (« Computer », « Printer », ...) — voir <see cref="Services.FieldUnicity.FieldUnicityCatalog.ItemTypes"/>.</summary>
    public required string ItemType { get; set; }

    /// <summary>Un critère inactif reste configuré mais n'est plus contrôlé, comme dans GLPI.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>« Refuser la création » : l'objet en doublon n'est pas créé.</summary>
    public bool RefuseCreation { get; set; } = true;

    /// <summary>
    /// « Envoyer une notification » : publie l'événement <c>FieldUnicity/duplicate</c>, que des
    /// notifications et webhooks peuvent suivre — voir <c>NotificationEventCatalog</c>.
    ///
    /// Indépendant du refus : notifier sans refuser sert à repérer des doublons sans bloquer les
    /// imports le temps de nettoyer l'existant.
    /// </summary>
    public bool NotifyOnDuplicate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Champs dont la combinaison doit être unique. Un critère sans champ ne contrôle rien : c'est
    /// volontairement autorisé (le temps de préparer un critère), et le contrôle l'ignore alors.
    /// </summary>
    public List<FieldUnicityField> Fields { get; set; } = [];
}

/// <summary>
/// Un champ d'un <see cref="FieldUnicityCriterion"/> (la colonne <c>fields</c> de GLPI, qui y range
/// une liste séparée par des virgules). Table de liaison plutôt qu'une chaîne : c'est elle qui est
/// relue à chaque création d'objet, et un nom de champ renommé se retrouve par une requête plutôt
/// qu'en découpant du texte.
/// </summary>
public class FieldUnicityField
{
    public int Id { get; set; }

    public int FieldUnicityCriterionId { get; set; }
    public FieldUnicityCriterion? Criterion { get; set; }

    /// <summary>Nom de la propriété du modèle (« SerialNumber », « Name », ...).</summary>
    public required string FieldName { get; set; }
}
