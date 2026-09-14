using GlpiNg.Modules.Abstractions.Reports;

namespace GlpiNg.Web.Services;

/// <summary>
/// Vue unique sur les rapports de tous les modules : c'est ce que consultent la liste
/// <c>/tools/reports</c> et la page d'un rapport, pour n'avoir à connaître ni les modules
/// présents, ni lequel porte quelle clé.
///
/// L'hôte ne tient donc aucune table des rapports — un module qui arrive apporte les siens (voir
/// <see cref="IReportProvider"/>), un module retiré emporte les siens. Seul l'ordre d'affichage
/// des catégories est du ressort de l'hôte : c'est une décision de présentation, qu'aucun module
/// ne peut prendre pour les autres.
/// </summary>
public sealed class ReportCatalog(IEnumerable<IReportProvider> providers)
{
    /// <summary>
    /// Ordre canonique des catégories, calqué sur celui du menu latéral (le parc d'abord, les
    /// outils ensuite). Une catégorie inconnue — apportée par un module futur — est affichée à la
    /// suite, dans l'ordre où les fournisseurs la déclarent : elle apparaît, sans que l'hôte ait
    /// eu à la prévoir.
    /// </summary>
    private static readonly string[] CategoryOrder = ["Parc", "Logiciels", "Réseau", "Inventaire", "Déploiement"];

    public IReadOnlyList<ReportDefinition> All => [.. providers.SelectMany(provider => provider.GetReports())];

    public ReportDefinition? Find(string key)
        => All.FirstOrDefault(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Rapports groupés par catégorie, dans l'ordre d'affichage.</summary>
    public IReadOnlyList<(string Category, IReadOnlyList<ReportDefinition> Reports)> ByCategory()
        => [.. All
            .GroupBy(definition => definition.Category)
            .OrderBy(group => Array.IndexOf(CategoryOrder, group.Key) is int index && index >= 0 ? index : CategoryOrder.Length)
            .Select(group => (group.Key, (IReadOnlyList<ReportDefinition>)[.. group]))];

    public async Task<IReadOnlyList<ReportFilter>> GetFiltersAsync(string key, CancellationToken cancellationToken = default)
    {
        foreach (IReportProvider provider in providers)
        {
            if (provider.GetReports().Any(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                return await provider.GetFiltersAsync(key, cancellationToken);
            }
        }

        return [];
    }

    /// <summary>
    /// Exécute le rapport auprès du premier fournisseur qui le reconnaît, ou renvoie <c>null</c> si
    /// aucun ne le connaît — une clé d'URL inventée ou le rapport d'un module désactivé.
    /// </summary>
    public async Task<ReportResult?> RunAsync(string key, ReportParameters parameters, CancellationToken cancellationToken = default)
    {
        foreach (IReportProvider provider in providers)
        {
            ReportResult? result = await provider.RunAsync(key, parameters, cancellationToken);

            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}
