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
/// Deux pièges, tous deux rencontrés sur de vraies bases.
///
/// 1. <b>Le HTML est souvent échappé en base.</b> Beaucoup de GLPI rangent
///    <c>&amp;lt;p&amp;gt;Bonjour&amp;lt;/p&amp;gt;</c> plutôt que <c>&lt;p&gt;Bonjour&lt;/p&gt;</c>.
///    Un convertisseur HTML n'y voit alors aucune balise, seulement du texte : il recopie
///    l'échappement tel quel, et l'article s'affiche avec toutes ses balises en clair — d'autant
///    plus sûrement que le rendu Markdown de GlpiNg n'interprète jamais le HTML.
///    <see cref="Unescape"/> défait cet échappement avant toute conversion.
/// 2. <b>Une balise inconnue ne doit pas emporter son contenu.</b> <c>Drop</c> supprimait la
///    balise <i>et</i> ce qu'elle contenait : un paragraphe enveloppé dans un <c>&lt;font&gt;</c>
///    ou un <c>&lt;o:p&gt;</c> collé depuis Word disparaissait en silence. <c>Bypass</c> retire la
///    balise et convertit son contenu, ce qui reprend beaucoup plus de texte sans jamais recopier
///    de balise dans le résultat.
/// </summary>
public static class GlpiHtmlToMarkdown
{
    private static readonly ReverseMarkdown.Converter Converter = new(new ReverseMarkdown.Config
    {
        // Bypass et non Drop : voir la doc de la classe. La garantie qui comptait — ne jamais
        // laisser une balise brute arriver en base — tient toujours, Bypass ne recopiant aucune
        // balise ; seul le contenu textuel des éléments inconnus est repris.
        Tags = { Unknown = ReverseMarkdown.Config.UnknownTagsOption.Bypass },

        // Corollaire de Bypass : ce qu'on ne veut pas voir doit être écarté explicitement. Le
        // corps d'un <script> ou d'un <style> est du texte pour le convertisseur, et « bypasser »
        // la balise afficherait ce code au milieu de l'article. Il n'y est jamais exécuté (voir
        // MarkdownRenderer), mais il n'a rien à y faire.
        Html = { ExcludeSelectors = { "script", "style", "noscript", "iframe", "object", "embed" } },

        Formatting =
        {
            RemoveComments = true,
            CleanupSpaces = true,

            // GLPI enveloppe volontiers chaque ligne dans un <div> : sans cela, un article
            // ordinaire ressort avec une ligne vide sur deux.
            SuppressDivNewlines = true,
        },

        Links = { SmartHref = true },

        Tables =
        {
            // Un tableau sans ligne d'en-tête est courant dans les articles repris (mise en page
            // plutôt que données) : lui en inventer une vide vaut mieux que de perdre le tableau,
            // le Markdown n'ayant pas de tableau sans en-tête.
            WithoutHeaderRow = ReverseMarkdown.Config.TableWithoutHeaderRowHandlingOption.EmptyRow,
            HeaderColumnSpans = true,
        },

        GithubFlavored = true,
    });

    /// <summary>Balises restantes, pour le repli : ce qui est entre chevrons est retiré.</summary>
    private static readonly Regex AnyTag = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    /// Commentaire HTML vide que ReverseMarkdown pose en guise de cellule d'en-tête quand il
    /// invente la ligne manquante d'un tableau (voir <c>WithoutHeaderRow</c>). C'est une
    /// convention interne au convertisseur, pas du Markdown : le rendu de GlpiNg n'interprétant
    /// aucun HTML, elle s'afficherait telle quelle en haut du tableau.
    /// </summary>
    private const string EmptyHeaderCellMarker = "<!---->";

    /// <summary>Une vraie balise ouvrante ou fermante — le signe que le HTML n'est pas échappé.</summary>
    private static readonly Regex RealTag = new("<\\s*/?[a-zA-Z][^>]*>", RegexOptions.Compiled);

    /// <summary>Une balise échappée (<c>&amp;lt;p&amp;gt;</c>), éventuellement plusieurs fois.</summary>
    private static readonly Regex EscapedTag = new("&(?:amp;)*lt;\\s*/?[a-zA-Z]", RegexOptions.Compiled);

    /// <summary>
    /// Nombre maximal de décodages successifs. Un contenu doublement échappé existe (une migration
    /// GLPI qui a ré-échappé ce qui l'était déjà) ; au-delà, on s'arrête plutôt que de décoder
    /// indéfiniment un texte qui parlerait littéralement d'entités HTML.
    /// </summary>
    private const int MaxUnescapePasses = 3;

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

        string source = Unescape(html);

        try
        {
            return Converter.Convert(source).Replace(EmptyHeaderCellMarker, string.Empty).Trim();
        }
        catch (Exception)
        {
            return WebUtility.HtmlDecode(AnyTag.Replace(source, " ")).Trim();
        }
    }

    /// <summary>
    /// Défait l'échappement HTML tant que le contenu ne porte <b>que</b> des balises échappées.
    ///
    /// La condition est volontairement stricte : dès qu'une vraie balise apparaît, le contenu est
    /// du HTML et le décodage s'arrête. Sans cela, un article qui montre légitimement du code —
    /// « écrivez <c>&amp;lt;VirtualHost&amp;gt;</c> dans la configuration » — verrait son exemple
    /// transformé en balise, puis avalé par le convertisseur.
    /// </summary>
    internal static string Unescape(string html)
    {
        string current = html;

        for (int pass = 0; pass < MaxUnescapePasses; pass++)
        {
            if (RealTag.IsMatch(current) || !EscapedTag.IsMatch(current))
            {
                break;
            }

            string decoded = WebUtility.HtmlDecode(current);

            if (decoded == current)
            {
                break;
            }

            current = decoded;
        }

        return current;
    }
}
