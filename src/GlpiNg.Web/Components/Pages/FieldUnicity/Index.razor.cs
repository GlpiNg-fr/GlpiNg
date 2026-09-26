using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.FieldUnicity;
using GlpiNg.Web.Services.FieldUnicity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.FieldUnicity;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<FieldUnicityCriterion> _criteria = [];
    private List<FieldUnicityCriterion> _filtered = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];

    private FieldUnicityCriterion _newCriterion = NewBlankCriterion();

    private bool AllSelected => _filtered.Count > 0 && _selectedIds.Count == _filtered.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newCriterion.Name);

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        _criteria = await db.FieldUnicityCriteria
            .AsNoTracking()
            .Include(criterion => criterion.Fields)
            .OrderBy(criterion => criterion.ItemType)
            .ThenBy(criterion => criterion.Name)
            .ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<FieldUnicityCriterion> query = term.Length == 0
            ? _criteria
            : _criteria.Where(criterion =>
                criterion.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || FieldUnicityCatalog.LabelFor(criterion.ItemType).Contains(term, StringComparison.OrdinalIgnoreCase));

        _filtered = [.. query];
        _selectedIds.IntersectWith(_filtered.Select(criterion => criterion.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (FieldUnicityCriterion criterion in _filtered)
            {
                _selectedIds.Add(criterion.Id);
            }
        }
    }

    private void ToggleSelect(int criterionId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(criterionId);
        }
        else
        {
            _selectedIds.Remove(criterionId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        List<FieldUnicityCriterion> toDelete = await db.FieldUnicityCriteria
            .Where(criterion => _selectedIds.Contains(criterion.Id))
            .ToListAsync();

        db.FieldUnicityCriteria.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task CreateAsync()
    {
        if (!CanCreate)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        if (await db.FieldUnicityCriteria.AsNoTracking()
                .AnyAsync(criterion => criterion.Name == _newCriterion.Name))
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Ce nom est déjà utilisé."));
            return;
        }

        db.FieldUnicityCriteria.Add(_newCriterion);
        await db.SaveChangesAsync();

        _newCriterion = NewBlankCriterion();

        await JS.InvokeVoidAsync("glping.hideModal", "newCriterionModal");
        await LoadAsync();
    }

    private static FieldUnicityCriterion NewBlankCriterion() => new()
    {
        Name = string.Empty,
        ItemType = FieldUnicityCatalog.ItemTypes[0].ItemType,
    };
}
