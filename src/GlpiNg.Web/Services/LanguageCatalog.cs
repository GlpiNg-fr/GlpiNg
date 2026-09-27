using System.Text.Json;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Services;

/// <summary>
/// Langues de l'interface — les quinze de « Valeurs par défaut », reprises de GLPI — et chargement
/// de leurs catalogues de traduction (voir <see cref="Tr"/>).
///
/// Les catalogues sont des fichiers JSON <c>i18n/{code}.json</c> (texte français → traduction),
/// un par langue et non par variante : les trois français n'en ont pas besoin (c'est la langue des
/// clés), les deux anglais partagent <c>en.json</c>, les deux portugais <c>pt.json</c>. Une variante
/// qui voudrait diverger n'aurait qu'à ajouter son propre fichier (<c>pt-BR.json</c>), consulté
/// avant celui de la langue.
/// </summary>
public static class LanguageCatalog
{
    /// <summary>Code GLPI (« fr_FR ») et nom de la langue dans cette langue.</summary>
    public static readonly IReadOnlyList<(string Code, string NativeName)> Languages =
    [
        ("fr_FR", "Français"),
        ("fr_CA", "Français (Canada)"),
        ("fr_BE", "Français (Belgique)"),
        ("en_GB", "English"),
        ("en_US", "English (US)"),
        ("es_ES", "Español (España)"),
        ("de_DE", "Deutsch"),
        ("it_IT", "Italiano"),
        ("pt_PT", "Português"),
        ("pt_BR", "Português do Brasil"),
        ("nl_NL", "Nederlands"),
        ("ru_RU", "Русский"),
        ("zh_CN", "简体中文"),
        ("ja_JP", "日本語"),
        ("ko_KR", "한국어"),
    ];

    /// <summary>Une langue inconnue retombe sur le français plutôt que sur une culture invalide.</summary>
    public static string Normalize(string? code) =>
        Languages.FirstOrDefault(language => string.Equals(language.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase)).Code
        ?? "fr_FR";

    public static string NativeNameOf(string? code) =>
        Languages.First(language => language.Code == Normalize(code)).NativeName;

    /// <summary>
    /// Charge tous les catalogues de <paramref name="directory"/> dans <see cref="Tr"/>. Un fichier
    /// illisible est signalé et ignoré : une langue mal traduite ne doit pas empêcher le démarrage,
    /// ses textes s'afficheront simplement en français.
    /// </summary>
    public static void LoadInto(string directory, ILogger logger)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> catalogs = new(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(directory))
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
            {
                string code = Path.GetFileNameWithoutExtension(file);

                try
                {
                    Dictionary<string, string>? entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                    if (entries is not null)
                    {
                        catalogs[code] = entries;
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Catalogue de traduction illisible, ignoré : {File}", file);
                }
            }
        }

        Tr.Load(catalogs);
        logger.LogInformation("Traductions chargées : {Catalogs}", string.Join(", ", catalogs.Select(c => $"{c.Key} ({c.Value.Count})")));
    }
}
