using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Groups;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private List<GlpiGroup> _groups = [];
    private List<GlpiGroup> _filteredGroups = [];
    private Dictionary<int, GlpiGroup> _byId = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private GlpiGroup _newGroup = NewBlankGroup();

    private bool AllSelected => _filteredGroups.Count > 0 && _selectedIds.Count == _filteredGroups.Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _groups = await db.Groups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .ToListAsync();

        _byId = _groups.ToDictionary(group => group.Id);
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

        IEnumerable<GlpiGroup> query = term.Length == 0
            ? _groups
            : _groups.Where(group => MatchesSearch(group, term));

        _filteredGroups = query.OrderBy(group => FullName(group)).ToList();
        _selectedIds.IntersectWith(_filteredGroups.Select(group => group.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (GlpiGroup group in _filteredGroups)
            {
                _selectedIds.Add(group.Id);
            }
        }
    }

    private void ToggleSelect(int groupId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(groupId);
        }
        else
        {
            _selectedIds.Remove(groupId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0) return;

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<GlpiGroup> toDelete = await db.Groups
            .Where(group => _selectedIds.Contains(group.Id))
            .ToListAsync();

        db.Groups.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task CreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(_newGroup.Name))
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        db.Groups.Add(_newGroup);
        await db.SaveChangesAsync();

        _newGroup = NewBlankGroup();
        await JS.InvokeVoidAsync("glpiNg.hideModal", "newGroupModal");
        await LoadAsync();
    }

    private static GlpiGroup NewBlankGroup() => new() { Name = string.Empty };

    private static bool MatchesSearch(GlpiGroup group, string term)
    {
        return group.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (group.Comment?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private string FullName(GlpiGroup group)
    {
        var parts = new List<string> { group.Name };
        var current = group;

        while (current.ParentId is { } parentId && _byId.TryGetValue(parentId, out var parent))
        {
            parts.Insert(0, parent.Name);
            current = parent;
        }

        return string.Join(" > ", parts);
    }
}
