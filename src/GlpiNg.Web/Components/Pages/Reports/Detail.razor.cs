using System.Globalization;
using GlpiNg.Modules.Abstractions.Reports;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Reports;

/// <summary>
/// Affiche un rapport, quel qu'il soit : ses critères de saisie, ses tableaux et ses exports. La
/// page ne connaît aucun rapport en particulier — elle rend ce que le module a calculé (voir
/// <see cref="ReportResult"/>), ce qui fait qu'un nouveau rapport n'a rien à ajouter ici.
/// </summary>
public partial class Detail : ComponentBase
{
    [Parameter]
    public string Key { get; set; } = string.Empty;

    [Inject]
    private ReportCatalog Catalog { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private ReportDefinition? _definition;
    private List<ReportFilter> _filters = [];

    /// <summary>Valeurs saisies dans les filtres, par clé de filtre.</summary>
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    private ReportResult? _result;
    private DateTime? _generatedAt;
    private bool _isRunning;

    /// <summary>
    /// Rapport déjà chargé, pour ne pas tout recalculer à chaque rendu : le paramètre de route
    /// change quand on passe d'un rapport à l'autre sans quitter la page, et c'est le seul moment
    /// où il faut repartir de zéro.
    /// </summary>
    private string? _loadedKey;

    protected override async Task OnParametersSetAsync()
    {
        if (string.Equals(_loadedKey, Key, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _loadedKey = Key;
        _definition = Catalog.Find(Key);
        _result = null;
        _generatedAt = null;
        _values.Clear();

        if (_definition is null)
        {
            _filters = [];
            return;
        }

        _filters = [.. await Catalog.GetFiltersAsync(_definition.Key)];

        foreach (ReportFilter filter in _filters)
        {
            _values[filter.Key] = filter.DefaultValue;
        }

        // Premier calcul sans attendre un clic : un rapport dont il faut demander l'exécution pour
        // voir quoi que ce soit donne l'impression d'être vide. Les filtres partent de leurs
        // valeurs par défaut, qui sont faites pour ça.
        await RunAsync();
    }

    private async Task RunAsync()
    {
        if (_definition is null)
        {
            return;
        }

        _isRunning = true;
        StateHasChanged();

        try
        {
            // Copie du dictionnaire : le rapport ne doit pas voir les valeurs changer sous lui si
            // l'utilisateur touche un filtre pendant le calcul.
            _result = await Catalog.RunAsync(_definition.Key, new ReportParameters(new Dictionary<string, string?>(_values)));
            _generatedAt = DateTime.Now;
        }
        finally
        {
            _isRunning = false;
        }
    }

    private string? ValueOf(ReportFilter filter)
        => _values.TryGetValue(filter.Key, out string? value) ? value : filter.DefaultValue;

    private void SetValue(string key, string? value) => _values[key] = value;

    /// <summary>
    /// Largeur de la barre d'une cellule « part », en pourcentage et en culture invariante : c'est
    /// du CSS, où un séparateur décimal français rendrait la règle invalide.
    /// </summary>
    private static string BarWidth(double share)
        => Math.Clamp(share * 100, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);

    private async Task ExportAsync(string format)
    {
        if (_definition is null || _result is null)
        {
            return;
        }

        (byte[] Bytes, string Extension, string ContentType) export = format switch
        {
            "pdf-landscape" => (ReportExportWriter.BuildPdf(_definition, _result, landscape: true), "pdf", "application/pdf"),
            "pdf-portrait" => (ReportExportWriter.BuildPdf(_definition, _result, landscape: false), "pdf", "application/pdf"),
            "csv" => (ReportExportWriter.BuildCsv(_result), "csv", "text/csv"),
            "ods" => (ReportExportWriter.BuildOds(_result), "ods", "application/vnd.oasis.opendocument.spreadsheet"),
            "xlsx" => (ReportExportWriter.BuildXlsx(_result), "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

        string fileName = $"rapport-{_definition.Key}.{export.Extension}";

        using MemoryStream stream = new(export.Bytes);
        using DotNetStreamReference streamRef = new(stream);
        await JS.InvokeVoidAsync("glpiNg.downloadFileFromStream", fileName, export.ContentType, streamRef);
    }
}
