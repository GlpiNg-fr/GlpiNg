using System.Globalization;

namespace GlpiNg.Web.Models.CustomAssets;

/// <summary>
/// Type d'objet, au sens des références polymorphes de GLPI (documents, notes, historique), pour un
/// type d'actif personnalisé.
///
/// Préfixé pour qu'un type nommé « Computer » par un administrateur ne vienne jamais se confondre
/// avec les ordinateurs du parc : les documents et les notes rangent ce texte tel quel, et deux
/// types portant la même chaîne partageraient leurs pièces jointes.
/// </summary>
public static class CustomAssetItemTypes
{
    public const string Prefix = "CustomAsset.";

    public static string For(string systemName) => Prefix + systemName;

    public static string For(CustomAssetDefinition definition) => For(definition.SystemName);
}

/// <summary>
/// Conversion des valeurs de champ personnalisé entre leur forme stockée (toujours du texte, voir
/// <see cref="CustomAssetValue"/>) et leur forme affichée.
///
/// Tout passe par la culture invariante : une valeur saisie sur un poste en français et relue sur
/// un serveur en anglais doit rester la même date et le même nombre.
/// </summary>
public static class CustomAssetValueFormat
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm";

    /// <summary>Valeur telle que la rend un champ de formulaire HTML (input date, number, checkbox...).</summary>
    public static string ToInput(CustomAssetFieldType type, string? stored) => type switch
    {
        CustomAssetFieldType.Boolean => IsTrue(stored) ? "true" : "false",
        _ => stored ?? string.Empty,
    };

    public static bool IsTrue(string? stored) =>
        bool.TryParse(stored, out bool parsed) && parsed;

    /// <summary>
    /// Valeur affichée sur une fiche ou dans une liste. <paramref name="dropdownName"/> résout le
    /// libellé d'un intitulé à partir de son identifiant — la valeur stockée pour une liste
    /// déroulante étant l'identifiant, pour qu'un intitulé renommé le reste partout.
    /// </summary>
    public static string ToDisplay(CustomAssetField field, string? stored, Func<int, string?>? dropdownName = null)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return string.Empty;
        }

        return field.Type switch
        {
            CustomAssetFieldType.Boolean => IsTrue(stored) ? "Oui" : "Non",

            CustomAssetFieldType.Date => DateTime.TryParse(stored, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime date)
                ? date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)
                : stored,

            CustomAssetFieldType.DateTime => DateTime.TryParse(stored, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime moment)
                ? moment.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)
                : stored,

            CustomAssetFieldType.Dropdown => int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                ? dropdownName?.Invoke(id) ?? stored
                : stored,

            _ => stored,
        };
    }

    /// <summary>
    /// Valeur normalisée avant enregistrement, ou <c>null</c> si la saisie est vide. Une saisie
    /// qu'on n'arrive pas à interpréter est gardée telle quelle plutôt que jetée : l'écran signale
    /// déjà le champ, et perdre ce que l'utilisateur a tapé serait pire que le stocker de travers.
    /// </summary>
    public static string? ToStorage(CustomAssetFieldType type, string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        input = input.Trim();

        return type switch
        {
            CustomAssetFieldType.Boolean => IsTrue(input) ? "true" : "false",

            CustomAssetFieldType.Number => decimal.TryParse(input, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal number)
                || decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out number)
                ? number.ToString(CultureInfo.InvariantCulture)
                : input,

            CustomAssetFieldType.Date => DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
                ? date.ToString(DateFormat, CultureInfo.InvariantCulture)
                : input,

            CustomAssetFieldType.DateTime => DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime moment)
                ? moment.ToString(DateTimeFormat, CultureInfo.InvariantCulture)
                : input,

            _ => input,
        };
    }

    public static string LabelFor(CustomAssetFieldType type) => type switch
    {
        CustomAssetFieldType.Text => "Texte",
        CustomAssetFieldType.LongText => "Texte long",
        CustomAssetFieldType.Number => "Nombre",
        CustomAssetFieldType.Boolean => "Oui/Non",
        CustomAssetFieldType.Date => "Date",
        CustomAssetFieldType.DateTime => "Date et heure",
        CustomAssetFieldType.Url => "Lien (URL)",
        CustomAssetFieldType.Dropdown => "Liste déroulante",
        _ => type.ToString(),
    };
}
