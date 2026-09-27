namespace GlpiNg.Web.Services;

/// <summary>Couleurs d'un aperçu miniature de palette — voir <see cref="PaletteCatalog.Previews"/>.</summary>
public sealed record PalettePreview(string MenuBg, string MenuFg, string Accent, string Primary, string PrimaryFg, string Page);

/// <summary>
/// Les palettes de couleur proposées (« Palette de couleur » de GLPI) : leur libellé, tel que le
/// stockent la configuration (DefaultValuesSettings) et le compte (GlpiUser), leur clé CSS (voir
/// glping-theme.css, qui les définit sous <c>data-glping-theme</c>), et de quoi en dessiner un aperçu.
/// Un seul endroit, pour que la liste des Valeurs par défaut, les vignettes des préférences et la
/// résolution des préférences ne divergent jamais.
/// </summary>
public static class PaletteCatalog
{
    public static readonly IReadOnlyList<(string Label, string Key)> Palettes =
    [
        ("Auror", "auror"),
        ("Classic", "classic"),
        ("Dark", "dark"),
        ("Darker", "darker"),
        ("Midnight", "midnight"),
        ("Light Blue", "lightblue"),
        ("Vintage", "vintage"),
        ("Ice Cream", "icecream"),
    ];

    /// <summary>
    /// Couleurs des aperçus : fond et texte du menu, accent, primaire, texte sur la primaire et fond
    /// de page. Mêmes valeurs que glping-theme.css — un aperçu qui mentirait sur la palette serait
    /// pire que pas d'aperçu.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, PalettePreview> Previews = new Dictionary<string, PalettePreview>
    {
        ["auror"] = new("#1b2f62", "#f4f6fa", "#fec95c", "#3a5693", "#fff", "#fff"),
        ["classic"] = new("#98a458", "#000", "#fff", "#f2b265", "#8f5a0a", "#fff"),
        ["dark"] = new("#161514", "#fff", "#c4c0bb", "#585957", "#fff", "#fff"),
        ["darker"] = new("#242323", "#f4f6fa", "#c8c8c8", "#6b6b6b", "#f3f3f3", "#1f1e1e"),
        ["midnight"] = new("#000", "#e5e7eb", "#b6c3e0", "#b6c3e0", "#232e3c", "#000"),
        ["lightblue"] = new("#1a7ab2", "#f4f6fa", "#fff", "#c8eafd", "#1a4f73", "#fff"),
        ["vintage"] = new("#064260", "#fff", "#f2ef9f", "#f9201d", "#fff", "#fff"),
        ["icecream"] = new("#113f59", "#f3edd3", "#19bec0", "#d54f58", "#f3edd3", "#fff"),
    };

    /// <summary>Clé CSS d'une palette ; une valeur inconnue retombe sur Auror plutôt que sur une page sans couleurs.</summary>
    public static string KeyOf(string? label) =>
        Palettes.FirstOrDefault(palette => string.Equals(palette.Label, label?.Trim(), StringComparison.OrdinalIgnoreCase)).Key
        ?? "auror";

    /// <summary>Libellé normalisé d'une palette (« light blue » → « Light Blue »).</summary>
    public static string LabelOf(string? label) =>
        Palettes.First(palette => palette.Key == KeyOf(label)).Label;
}
