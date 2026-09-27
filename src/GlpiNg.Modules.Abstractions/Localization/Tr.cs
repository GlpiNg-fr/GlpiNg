using System.Globalization;

namespace GlpiNg.Modules.Abstractions.Localization;

/// <summary>
/// Traduction de l'interface, à la manière de gettext dans GLPI : la clé est le texte français
/// lui-même (<c>T("Rechercher")</c>), et un catalogue par langue en donne la traduction. Un texte
/// absent du catalogue s'affiche tel quel, en français — jamais une clé technique ni un vide.
///
/// Statique et adossé à <see cref="CultureInfo.CurrentUICulture"/> plutôt qu'injecté : les
/// libellés vivent aussi dans des classes statiques (statuts, priorités…) qui n'ont pas accès à
/// l'injection de dépendances. La culture d'interface est posée par l'hôte à chaque requête et à
/// chaque circuit Blazor, d'après la préférence de langue (voir UserPreferencesPreloader), et suit le
/// flux asynchrone : chaque utilisateur lit donc sa langue, même en parallèle.
///
/// Usage : <c>@T("Enregistrer")</c> dans le balisage (voir les _Imports.razor), <c>Tr.T(...)</c>
/// dans le code. Avec des valeurs : <c>T("Supprimer {0} élément(s) ?", count)</c> — le catalogue
/// traduit le modèle, puis les valeurs y sont insérées.
/// </summary>
public static class Tr
{
    /// <summary>Langue → (texte français → traduction). Remplacé en bloc au chargement.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _catalogs =
        new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>Installe les catalogues (appelé une fois par l'hôte au démarrage).</summary>
    /// <param name="catalogs">Par code de catalogue (« en », « pt », « zh »…).</param>
    public static void Load(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogs) =>
        _catalogs = catalogs;

    /// <summary>Codes des catalogues chargés — pour les diagnostics et les tests.</summary>
    public static IEnumerable<string> LoadedCatalogs => _catalogs.Keys;

    public static string T(string french)
    {
        if (string.IsNullOrEmpty(french))
        {
            return french;
        }

        IReadOnlyDictionary<string, string>? catalog = CatalogFor(CultureInfo.CurrentUICulture);

        return catalog is not null && catalog.TryGetValue(french, out string? translated) && !string.IsNullOrEmpty(translated)
            ? translated
            : french;
    }

    /// <summary>
    /// Modèle traduit puis complété par <paramref name="args"/> (<c>{0}</c>, <c>{1}</c>…). Un
    /// catalogue qui aurait abîmé les emplacements retombe sur le modèle français plutôt que de
    /// faire tomber la page.
    /// </summary>
    public static string T(string french, params object?[] args)
    {
        string template = T(french);

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, french, args);
        }
    }

    /// <summary>
    /// Catalogue d'une culture : d'abord la culture précise (« pt-BR »), puis sa langue (« pt »).
    /// Le français n'a pas de catalogue : c'est la langue des clés.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? CatalogFor(CultureInfo culture)
    {
        if (culture.TwoLetterISOLanguageName == "fr")
        {
            return null;
        }

        return _catalogs.TryGetValue(culture.Name, out IReadOnlyDictionary<string, string>? exact)
            ? exact
            : _catalogs.GetValueOrDefault(culture.TwoLetterISOLanguageName);
    }
}
