using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.CustomAssets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.CustomAssets;

public partial class AssetList : ComponentBase
{
    [Parameter]
    public string SystemName { get; set; } = string.Empty;

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private CustomAssetDefinition? _definition;
    private List<CustomAssetField> _fields = [];
    private List<CustomAsset> _assets = [];
    private List<CustomAsset> _filtered = [];
    private readonly HashSet<int> _selectedIds = [];
    private string _searchTerm = string.Empty;

    /// <summary>Intitulés par catégorie, chargés une fois pour toutes les listes déroulantes de l'écran.</summary>
    private Dictionary<DropdownType, List<DropdownItem>> _dropdowns = [];

    private string _newName = string.Empty;
    private string? _newComment;
    private readonly Dictionary<int, string?> _newValues = [];
    private string? _createError;

    private bool AllSelected => _filtered.Count > 0 && _selectedIds.Count == _filtered.Count;

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        _definition = await db.CustomAssetDefinitions
            .AsNoTracking()
            .Include(definition => definition.Fields)
            .FirstOrDefaultAsync(definition => definition.SystemName == SystemName);

        if (_definition is null)
        {
            _assets = [];
            _filtered = [];
            return;
        }

        _fields = [.. _definition.Fields.OrderBy(field => field.SortOrder)];

        _assets = await db.CustomAssets
            .AsNoTracking()
            .Include(asset => asset.Values)
            .Where(asset => asset.CustomAssetDefinitionId == _definition.Id)
            .OrderBy(asset => asset.Name)
            .ToListAsync();

        await LoadDropdownsAsync(db);

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task LoadDropdownsAsync(GlpiNgDbContext db)
    {
        List<DropdownType> types = [.. _fields
            .Where(field => field.Type == CustomAssetFieldType.Dropdown && field.DropdownType is not null)
            .Select(field => field.DropdownType!.Value)
            .Distinct()];

        _dropdowns = types.Count == 0
            ? []
            : (await db.Set<DropdownItem>()
                .AsNoTracking()
                .Where(item => types.Contains(item.Type))
                .OrderBy(item => item.Name)
                .ToListAsync())
                .GroupBy(item => item.Type)
                .ToDictionary(group => group.Key, group => group.ToList());
    }

    private IReadOnlyList<DropdownItem> OptionsFor(CustomAssetField field) =>
        field.DropdownType is { } type && _dropdowns.TryGetValue(type, out List<DropdownItem>? options)
            ? options
            : [];

    private string DisplayValue(CustomAsset asset, CustomAssetField field)
    {
        string? stored = asset.Values.FirstOrDefault(value => value.CustomAssetFieldId == field.Id)?.Value;
        return CustomAssetValueFormat.ToDisplay(field, stored, DropdownName(field));
    }

    private Func<int, string?> DropdownName(CustomAssetField field) =>
        id => OptionsFor(field).FirstOrDefault(option => option.Id == id)?.Name;

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        _filtered = term.Length == 0
            ? _assets
            // La recherche porte sur ce qui est affiché, valeurs de champs comprises : c'est ce que
            // l'utilisateur a sous les yeux, et un identifiant d'intitulé ne lui dirait rien.
            : [.. _assets.Where(asset =>
                asset.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (asset.Comment?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || _fields.Any(field => DisplayValue(asset, field).Contains(term, StringComparison.OrdinalIgnoreCase)))];

        _selectedIds.IntersectWith(_filtered.Select(asset => asset.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (CustomAsset asset in _filtered)
            {
                _selectedIds.Add(asset.Id);
            }
        }
    }

    private void ToggleSelect(int assetId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(assetId);
        }
        else
        {
            _selectedIds.Remove(assetId);
        }
    }

    private string? GetNewValue(CustomAssetField field) =>
        _newValues.TryGetValue(field.Id, out string? value) ? value : null;

    private void SetNewValue(CustomAssetField field, string? value) => _newValues[field.Id] = value;

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        List<CustomAsset> toDelete = await db.CustomAssets
            .Where(asset => _selectedIds.Contains(asset.Id))
            .ToListAsync();

        db.CustomAssets.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task CreateAsync()
    {
        if (_definition is null || string.IsNullOrWhiteSpace(_newName))
        {
            return;
        }

        if (_fields.FirstOrDefault(field => field.IsMandatory && string.IsNullOrWhiteSpace(GetNewValue(field))) is { } missing)
        {
            _createError = $"Le champ « {missing.Label} » est obligatoire.";
            return;
        }

        _createError = null;

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        CustomAsset asset = new()
        {
            CustomAssetDefinitionId = _definition.Id,
            Name = _newName.Trim(),
            Comment = _newComment,
        };

        foreach (CustomAssetField field in _fields)
        {
            string? stored = CustomAssetValueFormat.ToStorage(field.Type, GetNewValue(field));
            if (stored is null)
            {
                continue;
            }

            asset.Values.Add(new CustomAssetValue
            {
                CustomAssetFieldId = field.Id,
                Value = stored,
            });
        }

        db.CustomAssets.Add(asset);
        await db.SaveChangesAsync();

        _newName = string.Empty;
        _newComment = null;
        _newValues.Clear();

        await JS.InvokeVoidAsync("glpiNg.hideModal", "newAssetModal");
        await LoadAsync();
    }
}
