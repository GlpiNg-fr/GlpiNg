using System.Security.Cryptography;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.OAuthClients;

public partial class Index : ComponentBase
{
    private static readonly PasswordHasher<OAuthClient> SecretHasher = new();

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private List<OAuthClient> _clients = [];
    private List<OAuthClient> _filteredClients = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private OAuthClient _newClient = NewBlankClient();
    private string? _createError;
    private string? _revealSecret;

    private bool AllSelected => _filteredClients.Count > 0 && _selectedIds.Count == _filteredClients.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newClient.Name);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _clients = await db.OAuthClients.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<OAuthClient> query = term.Length == 0
            ? _clients
            : _clients.Where(c => c.Name.Contains(term, StringComparison.OrdinalIgnoreCase));

        _filteredClients = query.OrderBy(c => c.Name).ToList();
        _selectedIds.IntersectWith(_filteredClients.Select(c => c.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (OAuthClient client in _filteredClients)
            {
                _selectedIds.Add(client.Id);
            }
        }
    }

    private void ToggleSelect(int clientId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(clientId);
        }
        else
        {
            _selectedIds.Remove(clientId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<OAuthClient> toDelete = await db.OAuthClients
            .Where(c => _selectedIds.Contains(c.Id))
            .ToListAsync();

        db.OAuthClients.RemoveRange(toDelete);
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

        bool alreadyExists = await db.OAuthClients.AsNoTracking().AnyAsync(c => c.Name == _newClient.Name);
        if (alreadyExists)
        {
            _createError = "Ce nom est déjà utilisé.";
            return;
        }

        _newClient.ClientId = RandomNumberGenerator.GetHexString(64, lowercase: true);
        string plainSecret = RandomNumberGenerator.GetHexString(48, lowercase: true);
        _newClient.ClientSecretHash = SecretHasher.HashPassword(_newClient, plainSecret);

        db.OAuthClients.Add(_newClient);
        await db.SaveChangesAsync();

        _newClient = NewBlankClient();
        _createError = null;

        await JS.InvokeVoidAsync("glpiNg.hideModal", "newOAuthClientModal");
        await LoadAsync();

        _revealSecret = plainSecret;
    }

    private async Task CopySecretAsync()
    {
        if (_revealSecret is not null)
        {
            await JS.InvokeVoidAsync("glpiNg.copyToClipboard", _revealSecret);
        }
    }

    private Task CloseRevealAsync()
    {
        _revealSecret = null;
        return Task.CompletedTask;
    }

    private static OAuthClient NewBlankClient() => new()
    {
        Name = string.Empty,
        ClientId = string.Empty,
        ClientSecretHash = string.Empty,
    };

    private static string GrantsLabel(OAuthGrantTypes grants) => grants == OAuthGrantTypes.None
        ? "—"
        : string.Join(", ", Enum.GetValues<OAuthGrantTypes>().Where(g => g != OAuthGrantTypes.None && grants.HasFlag(g)));

    private static string ScopesLabel(OAuthScopes scopes) => scopes == OAuthScopes.None
        ? "—"
        : string.Join(", ", Enum.GetValues<OAuthScopes>().Where(s => s != OAuthScopes.None && scopes.HasFlag(s)));
}
