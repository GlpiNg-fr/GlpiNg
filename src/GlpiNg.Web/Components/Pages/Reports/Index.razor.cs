using GlpiNg.Modules.Abstractions.Reports;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;

namespace GlpiNg.Web.Components.Pages.Reports;

/// <summary>
/// Liste des rapports disponibles, groupés par catégorie — l'équivalent de « Outils &gt; Rapports »
/// de GLPI. Rien n'y est codé en dur : la liste est celle que les modules contribuent (voir
/// <see cref="ReportCatalog"/>), et un module désactivé fait simplement disparaître ses rapports.
/// </summary>
public partial class Index : ComponentBase
{
    [Inject]
    private ReportCatalog Catalog { get; set; } = null!;

    private string _search = string.Empty;
    private List<(string Category, List<ReportDefinition> Reports)> _visibleGroups = [];

    private string MatchCountLabel
    {
        get
        {
            int count = _visibleGroups.Sum(group => group.Reports.Count);
            return count > 1 ? $"{count} rapports" : $"{count} rapport";
        }
    }

    protected override void OnInitialized() => ApplyFilter();

    private void OnSearchInput(string? value)
    {
        _search = value ?? string.Empty;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string term = _search.Trim();

        _visibleGroups = [.. Catalog.ByCategory()
            .Select(group => (group.Category, Reports: group.Reports.Where(report => Matches(report, term)).ToList()))
            .Where(group => group.Reports.Count > 0)];
    }

    /// <summary>
    /// La recherche porte aussi sur la description et la catégorie : un rapport se cherche par ce
    /// qu'on veut savoir (« logiciels », « lieu », « échecs ») bien plus souvent que par son titre
    /// exact.
    /// </summary>
    private static bool Matches(ReportDefinition report, string term)
        => term.Length == 0
            || report.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || report.Description.Contains(term, StringComparison.CurrentCultureIgnoreCase)
            || report.Category.Contains(term, StringComparison.CurrentCultureIgnoreCase);
}
