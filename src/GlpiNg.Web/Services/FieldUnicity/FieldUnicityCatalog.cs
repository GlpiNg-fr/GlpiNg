using System.Collections.Concurrent;
using GlpiNg.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GlpiNg.Web.Services.FieldUnicity;

/// <summary>Un champ proposé à l'écran pour composer un critère d'unicité.</summary>
/// <param name="FieldName">Nom de la propriété du modèle, tel qu'enregistré sur le critère.</param>
/// <param name="Label">Libellé affiché.</param>
public sealed record FieldUnicityFieldOption(string FieldName, string Label);

/// <summary>
/// Ce sur quoi un critère d'unicité peut porter : les types d'objets proposés, et pour chacun les
/// champs disponibles.
///
/// Les types sont énumérés à la main parce que la liste doit correspondre aux endroits où le
/// contrôle est réellement appliqué (voir les appels à <see cref="FieldUnicityService"/>) : proposer
/// un type dont aucune création ne passe par le contrôle donnerait un critère qui ne se déclenche
/// jamais, sans que rien ne le dise. Les champs, eux, sont lus dans le modèle EF Core plutôt que
/// recopiés : une propriété renommée ou ajoutée suit sans qu'on y pense, là où une liste figée
/// aurait fini par mentir.
///
/// Seuls les champs texte sont proposés : le contrôle compare des valeurs par égalité SQL
/// (voir <see cref="FieldUnicityService"/>), ce qui se traduit proprement pour du texte, et les
/// champs qui comptent pour l'unicité d'un actif — nom, numéro de série, numéro d'inventaire,
/// UUID, adresse MAC — en sont tous.
/// </summary>
public sealed class FieldUnicityCatalog(IDbContextFactory<GlpiNgDbContext> dbFactory)
{
    /// <summary>
    /// Types d'objets sur lesquels un critère peut porter, dans l'ordre d'affichage. Ordinateur en
    /// tête : c'est le seul alimenté automatiquement (import d'inventaire), donc celui où les
    /// doublons arrivent sans qu'on les saisisse.
    /// </summary>
    public static readonly (string ItemType, string Label)[] ItemTypes =
    [
        (Modules.Abstractions.Items.ItemTypes.Computer, "Ordinateur"),
        (Modules.Abstractions.Items.ItemTypes.Printer, "Imprimante"),
        (Modules.Abstractions.Items.ItemTypes.Peripheral, "Périphérique"),
        (Modules.Abstractions.Items.ItemTypes.NetworkEquipment, "Matériel réseau"),
        (Modules.Abstractions.Items.ItemTypes.Phone, "Téléphone"),
        (Modules.Abstractions.Items.ItemTypes.Rack, "Baie"),
        (Modules.Abstractions.Items.ItemTypes.Enclosure, "Châssis"),
        (Modules.Abstractions.Items.ItemTypes.Pdu, "PDU"),
        (Modules.Abstractions.Items.ItemTypes.PassiveEquipment, "Équipement passif"),
        (Modules.Abstractions.Items.ItemTypes.SimCard, "Carte SIM"),
        (Modules.Abstractions.Items.ItemTypes.Cable, "Câble"),
        (Modules.Abstractions.Items.ItemTypes.CartridgeItem, "Cartouche"),
        (Modules.Abstractions.Items.ItemTypes.ConsumableItem, "Consommable"),
    ];

    /// <summary>
    /// Champs exclus des propositions : un mot de passe ou une empreinte ne décrit pas l'identité
    /// d'un objet, et les faire ressortir dans une notification de doublon serait une fuite.
    /// </summary>
    private static readonly string[] ExcludedFieldMarkers =
        ["Password", "Passphrase", "Secret", "Token", "Hash", "Json", "Xml"];

    private static readonly Dictionary<string, string> FieldLabels = new(StringComparer.Ordinal)
    {
        ["Name"] = "Nom",
        ["SerialNumber"] = "Numéro de série",
        ["Serial"] = "Numéro de série",
        ["InventoryNumber"] = "Numéro d'inventaire",
        ["OtherSerial"] = "Numéro d'inventaire",
        ["Comment"] = "Commentaire",
        ["Manufacturer"] = "Fabricant",
        ["Model"] = "Modèle",
        ["Type"] = "Type",
        ["HardwareUuid"] = "UUID matériel",
        ["Uuid"] = "UUID",
        ["Domain"] = "Domaine",
        ["OperatingSystem"] = "Système d'exploitation",
        ["OsVersion"] = "Version du système",
        ["AssignedUser"] = "Utilisateur",
        ["LastLoggedUser"] = "Dernier utilisateur connecté",
        ["Contact"] = "Contact",
        ["ContactNumber"] = "Numéro du contact",
        ["MacAddress"] = "Adresse MAC",
        ["IpAddress"] = "Adresse IP",
        ["Site"] = "Site",
        ["Building"] = "Bâtiment",
        ["Room"] = "Salle",
        ["Reference"] = "Référence",
        ["Msisdn"] = "Numéro d'appel",
        ["Iccid"] = "ICCID",
        ["Imsi"] = "IMSI",
        ["Firmware"] = "Micrologiciel",
        ["Version"] = "Version",
    };

    /// <summary>
    /// Le modèle EF Core ne change pas en cours d'exécution : une lecture par type suffit pour tout
    /// le processus. Cache statique parce que ce service suit la durée de vie de
    /// <c>IDbContextFactory</c>, qui est ici enregistré par requête — sans cela, chaque ouverture de
    /// l'écran relirait le modèle.
    /// </summary>
    private static readonly ConcurrentDictionary<string, IReadOnlyList<FieldUnicityFieldOption>> FieldsByItemType = new(StringComparer.Ordinal);

    public static string LabelFor(string itemType) =>
        ItemTypes.FirstOrDefault(entry => entry.ItemType == itemType).Label is { Length: > 0 } label
            ? label
            : itemType;

    public static string LabelForField(string fieldName) =>
        FieldLabels.TryGetValue(fieldName, out string? label) ? label : fieldName;

    /// <summary>
    /// Champs proposables pour ce type, triés par libellé. Liste vide si le type n'est pas (ou
    /// plus) présent dans le modèle : mieux vaut un écran sans champ à cocher qu'une exception sur
    /// un critère devenu orphelin.
    /// </summary>
    public IReadOnlyList<FieldUnicityFieldOption> FieldsFor(string itemType) =>
        FieldsByItemType.GetOrAdd(itemType, ReadFieldsFromModel);

    private IReadOnlyList<FieldUnicityFieldOption> ReadFieldsFromModel(string itemType)
    {
        using GlpiNgDbContext db = dbFactory.CreateDbContext();

        // Comparaison insensible à la casse : les noms de types sont ceux de GLPI, dont certains ne
        // suivent pas la casse de la classe correspondante (« PDU » pour la classe Pdu).
        IEntityType? entityType = db.Model.GetEntityTypes()
            .FirstOrDefault(candidate => string.Equals(candidate.ClrType.Name, itemType, StringComparison.OrdinalIgnoreCase));

        if (entityType is null)
        {
            return [];
        }

        return
        [
            .. entityType.GetProperties()
                .Where(property => property.ClrType == typeof(string)
                                   && !property.IsShadowProperty()
                                   && !property.IsPrimaryKey()
                                   && !property.IsForeignKey()
                                   && !ExcludedFieldMarkers.Any(marker =>
                                       property.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                .Select(property => new FieldUnicityFieldOption(property.Name, LabelForField(property.Name)))
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
        ];
    }
}
