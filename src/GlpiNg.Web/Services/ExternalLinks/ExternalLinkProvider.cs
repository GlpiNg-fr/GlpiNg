using System.Text;
using GlpiNg.Modules.Abstractions.ExternalLinks;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.ExternalLinks;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.ExternalLinks;

/// <summary>
/// Implémentation hôte de <see cref="IExternalLinkProvider"/> : lit les liens associés au type
/// d'objet demandé et remplace leurs balises.
///
/// Le cloisonnement par entité s'applique de lui-même : <see cref="ExternalLink"/> est
/// <see cref="Modules.Abstractions.Entities.IEntityScoped"/>, donc le filtre global du DbContext
/// écarte les liens hors périmètre sans qu'on ait à y penser ici.
/// </summary>
public class ExternalLinkProvider(IDbContextFactory<GlpiNgDbContext> dbFactory) : IExternalLinkProvider
{
    public async Task<IReadOnlyList<ResolvedExternalLink>> GetForItemAsync(string itemType,
        IReadOnlyDictionary<string, string?> tagValues, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<ExternalLink> links = await db.ExternalLinks
            .AsNoTracking()
            .Where(link => link.ItemTypes.Any(association => association.ItemType == itemType))
            .OrderBy(link => link.Name)
            .ToListAsync(cancellationToken);

        return
        [
            .. links
                .Select(link => new ResolvedExternalLink(
                    link.Id,
                    link.Name,
                    Substitute(link.Url, tagValues),
                    link.OpenInNewWindow))
                .Where(link => IsRenderableUrl(link.Url))
        ];
    }

    /// <summary>
    /// Écarte les URL dont le schéma exécuterait du code dans le navigateur du visiteur plutôt
    /// que d'ouvrir une ressource.
    ///
    /// Seuls ces trois schémas sont refusés, pas une liste blanche : <c>ssh:</c>, <c>rdp:</c>,
    /// <c>telnet:</c>, <c>vnc:</c> et les protocoles maison sont précisément ce qu'on vient
    /// chercher dans un lien externe. Un lien est configuré par un administrateur, mais il
    /// s'affiche pour tous ceux qui ouvrent la fiche — la configuration ne doit pas pouvoir
    /// servir de vecteur d'exécution.
    /// </summary>
    public static bool IsRenderableUrl(string url)
    {
        string trimmed = url.TrimStart();

        return !trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
               && !trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
               && !trimmed.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Remplace les <c>[BALISE]</c> connues par leur valeur, encodée pour une URL.
    ///
    /// L'encodage est indispensable : un nom de poste avec une espace ou un lieu accentué
    /// produirait sinon une URL invalide. Une balise inconnue est laissée telle quelle — c'est
    /// probablement un crochet voulu par l'auteur de l'URL, pas une balise mal tapée, et
    /// l'effacer rendrait l'URL silencieusement fausse.
    ///
    /// Une balise connue mais sans valeur devient une chaîne vide : l'URL est alors visiblement
    /// incomplète, ce qui se diagnostique, là où un <c>[SERIAL]</c> intact dans la barre
    /// d'adresse ressemble à une panne du navigateur.
    /// </summary>
    public static string Substitute(string url, IReadOnlyDictionary<string, string?> tagValues)
    {
        if (string.IsNullOrEmpty(url) || !url.Contains('[', StringComparison.Ordinal))
        {
            return url;
        }

        // Copie insensible à la casse : l'appelant n'a pas à connaître le comparateur attendu,
        // et [name] doit valoir [NAME] comme dans GLPI.
        Dictionary<string, string?> values = new(tagValues, StringComparer.OrdinalIgnoreCase);

        StringBuilder result = new(url.Length);
        int index = 0;

        while (index < url.Length)
        {
            int open = url.IndexOf('[', index);
            if (open < 0)
            {
                result.Append(url, index, url.Length - index);
                break;
            }

            int close = url.IndexOf(']', open + 1);
            if (close < 0)
            {
                result.Append(url, index, url.Length - index);
                break;
            }

            result.Append(url, index, open - index);

            string tag = url[(open + 1)..close];

            if (IsKnownTag(tag))
            {
                values.TryGetValue(tag, out string? value);
                result.Append(Uri.EscapeDataString(value ?? string.Empty));
            }
            else
            {
                result.Append(url, open, close - open + 1);
            }

            index = close + 1;
        }

        return result.ToString();
    }

    private static bool IsKnownTag(string tag) =>
        ExternalLinkTags.Catalog.Any(entry => string.Equals(entry.Tag, tag, StringComparison.OrdinalIgnoreCase));
}
