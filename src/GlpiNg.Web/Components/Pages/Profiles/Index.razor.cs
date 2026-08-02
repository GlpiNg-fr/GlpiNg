using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Profiles;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private List<GlpiProfile> _profiles = [];
    private List<GlpiProfile> _filteredProfiles = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private GlpiProfile _newProfile = NewBlankProfile();

    private bool AllSelected => _filteredProfiles.Count > 0 && _selectedIds.Count == _filteredProfiles.Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _profiles = await db.Profiles
            .AsNoTracking()
            .OrderBy(profile => profile.Name)
            .ToListAsync();

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

        IEnumerable<GlpiProfile> query = term.Length == 0
            ? _profiles
            : _profiles.Where(profile => MatchesSearch(profile, term));

        _filteredProfiles = query.OrderBy(profile => profile.Name).ToList();
        _selectedIds.IntersectWith(_filteredProfiles.Select(profile => profile.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (GlpiProfile profile in _filteredProfiles)
            {
                _selectedIds.Add(profile.Id);
            }
        }
    }

    private void ToggleSelect(int profileId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(profileId);
        }
        else
        {
            _selectedIds.Remove(profileId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0) return;

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<GlpiProfile> toDelete = await db.Profiles
            .Where(profile => _selectedIds.Contains(profile.Id))
            .ToListAsync();

        db.Profiles.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task CreateProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(_newProfile.Name))
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        db.Profiles.Add(_newProfile);
        await db.SaveChangesAsync();

        _newProfile = NewBlankProfile();
        await JS.InvokeVoidAsync("glpiNg.hideModal", "newProfileModal");
        await LoadAsync();
    }

    private static GlpiProfile NewBlankProfile() => new() { Name = string.Empty };

    private static bool MatchesSearch(GlpiProfile profile, string term)
    {
        return profile.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (profile.Comment?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
