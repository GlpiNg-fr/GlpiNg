using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.CustomAssets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.CustomAssets;

public partial class Index : ComponentBase
{
    /// <summary>Un nom technique tient lieu de segment d'URL et de type d'objet : lettres et chiffres seulement, commençant par une lettre.</summary>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex SystemNamePattern();

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private List<CustomAssetDefinition> _definitions = [];
    private List<CustomAssetDefinition> _filtered = [];
    private Dictionary<int, int> _assetCounts = [];
    private string _searchTerm = string.Empty;
    private string? _createError;

    private CustomAssetDefinition _newDefinition = NewBlankDefinition();

    private bool CanCreate =>
        !string.IsNullOrWhiteSpace(_newDefinition.LabelSingular)
        && !string.IsNullOrWhiteSpace(_newDefinition.LabelPlural)
        && SystemNamePattern().IsMatch(_newDefinition.SystemName);

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        _definitions = await db.CustomAssetDefinitions
            .AsNoTracking()
            .Include(definition => definition.Fields)
            .OrderBy(definition => definition.LabelSingular)
            .ToListAsync();

        _assetCounts = await db.CustomAssets
            .AsNoTracking()
            .GroupBy(asset => asset.CustomAssetDefinitionId)
            .Select(group => new { DefinitionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.DefinitionId, entry => entry.Count);

        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        _filtered = term.Length == 0
            ? _definitions
            : [.. _definitions.Where(definition =>
                definition.LabelSingular.Contains(term, StringComparison.OrdinalIgnoreCase)
                || definition.LabelPlural.Contains(term, StringComparison.OrdinalIgnoreCase)
                || definition.SystemName.Contains(term, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Propose un nom technique et un pluriel à partir du singulier, tant qu'ils n'ont pas été
    /// saisis à la main : la plupart des types se nomment ainsi, et le champ reste modifiable.
    /// </summary>
    private void OnSingularChanged(ChangeEventArgs args)
    {
        string previous = _newDefinition.LabelSingular;
        string singular = args.Value?.ToString() ?? string.Empty;

        if (_newDefinition.SystemName.Length == 0 || _newDefinition.SystemName == Slugify(previous))
        {
            _newDefinition.SystemName = Slugify(singular);
        }

        if (_newDefinition.LabelPlural.Length == 0 || _newDefinition.LabelPlural == Pluralize(previous))
        {
            _newDefinition.LabelPlural = Pluralize(singular);
        }

        _newDefinition.LabelSingular = singular;
    }

    private static string Pluralize(string singular) =>
        singular.Length == 0 || singular.EndsWith('s') || singular.EndsWith('x') ? singular : singular + "s";

    /// <summary>Retire accents, espaces et ponctuation : « Vidéoprojecteur » donne « Videoprojecteur ».</summary>
    private static string Slugify(string label)
    {
        StringBuilder builder = new(label.Length);

        foreach (char character in label.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(builder.Length == 0 ? char.ToUpperInvariant(character) : character);
            }
        }

        // Un nom ne peut pas commencer par un chiffre : il sert d'identifiant, pas de libellé.
        string slug = builder.ToString();
        return slug.Length > 0 && char.IsDigit(slug[0]) ? "Type" + slug : slug;
    }

    private async Task CreateAsync()
    {
        if (!CanCreate)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        if (await db.CustomAssetDefinitions.AsNoTracking()
                .AnyAsync(definition => definition.SystemName == _newDefinition.SystemName))
        {
            _createError = "Ce nom technique est déjà utilisé par un autre type.";
            return;
        }

        _createError = null;
        db.CustomAssetDefinitions.Add(_newDefinition);
        await db.SaveChangesAsync();

        _newDefinition = NewBlankDefinition();

        await JS.InvokeVoidAsync("glping.hideModal", "newDefinitionModal");
        await LoadAsync();
    }

    private static CustomAssetDefinition NewBlankDefinition() => new()
    {
        SystemName = string.Empty,
        LabelSingular = string.Empty,
        LabelPlural = string.Empty,
    };
}
