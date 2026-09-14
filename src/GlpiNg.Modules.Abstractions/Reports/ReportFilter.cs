namespace GlpiNg.Modules.Abstractions.Reports;

/// <summary>Nature du champ de saisie proposé pour un filtre, et donc du contrôle rendu par l'hôte.</summary>
public enum ReportFilterKind
{
    /// <summary>Liste déroulante alimentée par <see cref="ReportFilter.Options"/>.</summary>
    Select,

    /// <summary>Date seule (<c>&lt;input type="date"&gt;</c>), transmise au format <c>yyyy-MM-dd</c>.</summary>
    Date,

    /// <summary>Entier (<c>&lt;input type="number"&gt;</c>).</summary>
    Number,
}

/// <summary>Une valeur proposée par un filtre <see cref="ReportFilterKind.Select"/>.</summary>
public sealed record ReportFilterOption(string Value, string Label);

/// <summary>
/// Critère de saisie affiché au-dessus d'un rapport. Le module décrit ce qu'il sait filtrer,
/// l'hôte rend le formulaire correspondant et lui rend les valeurs saisies dans
/// <see cref="ReportParameters"/> : aucune page n'a à connaître les filtres d'un rapport
/// particulier.
///
/// Les options d'une liste déroulante sont demandées au module au moment d'ouvrir le rapport
/// (<see cref="IReportProvider.GetFiltersAsync"/>) et non déclarées avec le rapport lui-même :
/// elles viennent de la base (statuts, lieux, éditeurs de logiciels...) et changent donc d'une
/// consultation à l'autre.
///
/// <c>DefaultValue</c> est la valeur appliquée tant que l'utilisateur n'a rien choisi ;
/// <c>null</c> laisse le champ vide, et l'hôte ajoute alors à une liste déroulante une première
/// option neutre ("Tous") — un filtre sans défaut est donc un filtre facultatif.
/// </summary>
public sealed record ReportFilter(
    string Key,
    string Label,
    ReportFilterKind Kind,
    IReadOnlyList<ReportFilterOption> Options,
    string? DefaultValue = null)
{
    public static ReportFilter Select(string key, string label, IReadOnlyList<ReportFilterOption> options, string? defaultValue = null)
        => new(key, label, ReportFilterKind.Select, options, defaultValue);

    public static ReportFilter Date(string key, string label, DateTime? defaultValue = null)
        => new(key, label, ReportFilterKind.Date, [], defaultValue?.ToString("yyyy-MM-dd"));

    public static ReportFilter Number(string key, string label, int? defaultValue = null)
        => new(key, label, ReportFilterKind.Number, [], defaultValue?.ToString());
}
