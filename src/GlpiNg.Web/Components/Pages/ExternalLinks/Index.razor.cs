using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.ExternalLinks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Components.Pages.ExternalLinks;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<ExternalLink> _links = [];
    private List<ExternalLink> _filtered = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];

    private ExternalLink _newLink = NewBlankLink();

    private bool AllSelected => _filtered.Count > 0 && _selectedIds.Count == _filtered.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newLink.Name) && !string.IsNullOrWhiteSpace(_newLink.Url);

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _links = await db.ExternalLinks
            .AsNoTracking()
            .Include(link => link.ItemTypes)
            .OrderBy(link => link.Name)
            .ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<ExternalLink> query = term.Length == 0
            ? _links
            : _links.Where(link => link.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                                   || link.Url.Contains(term, StringComparison.OrdinalIgnoreCase));

        _filtered = query.OrderBy(link => link.Name).ToList();
        _selectedIds.IntersectWith(_filtered.Select(link => link.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (ExternalLink link in _filtered)
            {
                _selectedIds.Add(link.Id);
            }
        }
    }

    private void ToggleSelect(int linkId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(linkId);
        }
        else
        {
            _selectedIds.Remove(linkId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        List<ExternalLink> toDelete = await db.ExternalLinks.Where(link => _selectedIds.Contains(link.Id)).ToListAsync();
        db.ExternalLinks.RemoveRange(toDelete);
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

        if (await db.ExternalLinks.AsNoTracking().AnyAsync(link => link.Name == _newLink.Name))
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Ce nom est déjà utilisé.")));
            return;
        }

        db.ExternalLinks.Add(_newLink);
        await db.SaveChangesAsync();

        _newLink = NewBlankLink();

        await JS.InvokeVoidAsync("glping.hideModal", "newLinkModal");
        await LoadAsync();
    }

    private static ExternalLink NewBlankLink() => new()
    {
        Name = string.Empty,
        Url = string.Empty,
    };
}
