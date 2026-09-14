using System.Net;
using System.Text.RegularExpressions;

namespace GlpiNg.Web.Import;

/// <summary>
/// Traduit en Markdown le HTML que GLPI stocke dans ses articles de base de connaissances.
///
/// Isolé du service d'import parce que c'est la seule règle de l'import qui transforme le fond et
/// non la forme des identifiants : le contenu repris n'est pas celui d'origine, c'est sa
/// traduction. La regrouper ici la rend nommable, testable, et évite qu'une deuxième conversion
/// s'installe ailleurs avec d'autres réglages.
///
/// <c>UnknownTags.Drop</c> est le réglage qui compte : une balise que le convertisseur ne sait pas
/// traduire est retirée plutôt que recopiée telle quelle. Sans lui, du HTML brut — y compris un
/// <c>&lt;script&gt;</c> — se retrouverait dans un contenu que GlpiNg stocke comme du Markdown.
/// Il ne serait certes jamais interprété à l'affichage (voir <c>MarkdownRenderer</c>, qui
/// n'interprète aucun HTML), mais s'afficherait en clair au milieu de l'article.
/// </summary>
public static class GlpiHtmlToMarkdown
{
    private static readonly ReverseMarkdown.Converter Converter = new(new ReverseMarkdown.Config
    {
        Tags = { Unknown = ReverseMarkdown.Config.UnknownTagsOption.Drop },
        Formatting = { RemoveComments = true },
        Links = { SmartHref = true },
        GithubFlavored = true,
    });

    /// <summary>Balises restantes, pour le repli : ce qui est entre chevrons est retiré.</summary>
    private static readonly Regex AnyTag = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    /// Markdown équivalent, ou chaîne vide si l'article n'avait pas de réponse.
    ///
    /// Une conversion qui échoue ne fait pas échouer l'import : on retombe sur le texte dépouillé
    /// de ses balises. Perdre la mise en forme d'un article vaut mieux que perdre l'article, et
    /// mieux encore que laisser du HTML s'installer en base.
    /// </summary>
    public static string Convert(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        try
        {
            return Converter.Convert(html).Trim();
        }
        catch (Exception)
        {
            return WebUtility.HtmlDecode(AnyTag.Replace(html, " ")).Trim();
        }
    }
}
