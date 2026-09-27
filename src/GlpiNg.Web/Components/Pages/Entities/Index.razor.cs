using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Entities;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [Inject]
    private EntityDeletionGuard DeletionGuard { get; set; } = null!;

    private List<GlpiEntity> _entities = [];
    private List<GlpiEntity> _filteredEntities = [];
    private Dictionary<int, GlpiEntity> _byId = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private GlpiEntity _newEntity = NewBlankEntity();

    private bool AllSelected => _filteredEntities.Count > 0 && _selectedIds.Count == _filteredEntities.Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _entities = await db.Entities
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .ToListAsync();

        _byId = _entities.ToDictionary(entity => entity.Id);
        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync()
    {
        await LoadAsync();
    }

    private void OnSearchChanged(KeyboardEventArgs args)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<GlpiEntity> query = term.Length == 0
            ? _entities
            : _entities.Where(entity => MatchesSearch(entity, term));

        _filteredEntities = query.OrderBy(entity => FullName(entity)).ToList();
        _selectedIds.IntersectWith(_filteredEntities.Select(entity => entity.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (GlpiEntity entity in _filteredEntities)
            {
                _selectedIds.Add(entity.Id);
            }
        }
    }

    private void ToggleSelect(int entityId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(entityId);
        }
        else
        {
            _selectedIds.Remove(entityId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0) return;

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<GlpiEntity> toDelete = await db.Entities
            .Where(entity => _selectedIds.Contains(entity.Id))
            .ToListAsync();

        // Même contrôle préalable que sur la fiche : on refuse la sélection entière dès qu'une
        // entité est encore occupée, plutôt que d'en supprimer une partie puis d'échouer au milieu
        // sur une violation de clé étrangère.
        foreach (GlpiEntity entity in toDelete)
        {
            if (await DeletionGuard.DescribeBlockersAsync(entity) is { } blockers)
            {
                ToastService.Notify(new ToastMessage(ToastType.Danger, blockers));
                return;
            }
        }

        db.Entities.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task CreateEntityAsync()
    {
        if (string.IsNullOrWhiteSpace(_newEntity.Name))
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        db.Entities.Add(_newEntity);
        await db.SaveChangesAsync();

        _newEntity = NewBlankEntity();
        await JS.InvokeVoidAsync("glping.hideModal", "newEntityModal");
        await LoadAsync();
    }

    private static GlpiEntity NewBlankEntity() => new() { Name = string.Empty };

    private static bool MatchesSearch(GlpiEntity entity, string term)
    {
        return entity.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (entity.Comment?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private string FullName(GlpiEntity entity)
    {
        var parts = new List<string> { entity.Name };
        var current = entity;

        while (current.ParentId is { } parentId && _byId.TryGetValue(parentId, out var parent))
        {
            parts.Insert(0, parent.Name);
            current = parent;
        }

        return string.Join(" > ", parts);
    }
}
