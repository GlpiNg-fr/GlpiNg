using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Authentication.Mail;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<AuthMailServer> _servers = [];
    private List<AuthMailServer> _filteredServers = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private AuthMailServer _newServer = NewBlankServer();

    private bool AllSelected => _filteredServers.Count > 0 && _selectedIds.Count == _filteredServers.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newServer.Name) && !string.IsNullOrWhiteSpace(_newServer.Host);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _servers = await db.AuthMailServers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<AuthMailServer> query = term.Length == 0
            ? _servers
            : _servers.Where(s => MatchesSearch(s, term));

        _filteredServers = query.OrderBy(s => s.Name).ToList();
        _selectedIds.IntersectWith(_filteredServers.Select(s => s.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (AuthMailServer server in _filteredServers)
            {
                _selectedIds.Add(server.Id);
            }
        }
    }

    private void ToggleSelect(int serverId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(serverId);
        }
        else
        {
            _selectedIds.Remove(serverId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<AuthMailServer> toDelete = await db.AuthMailServers
            .Where(s => _selectedIds.Contains(s.Id))
            .ToListAsync();

        db.AuthMailServers.RemoveRange(toDelete);
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

        bool alreadyExists = await db.AuthMailServers.AsNoTracking().AnyAsync(s => s.Name == _newServer.Name);
        if (alreadyExists)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Ce nom est déjà utilisé."));
            return;
        }

        db.AuthMailServers.Add(_newServer);
        await db.SaveChangesAsync();

        _newServer = NewBlankServer();

        await JS.InvokeVoidAsync("glpiNg.hideModal", "newMailServerModal");
        await LoadAsync();
    }

    private static AuthMailServer NewBlankServer() => new() { Name = string.Empty, Host = string.Empty };

    private static bool MatchesSearch(AuthMailServer server, string term) =>
        server.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || server.Host.Contains(term, StringComparison.OrdinalIgnoreCase);
}
