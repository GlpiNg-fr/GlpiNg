using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Models.Webhooks;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Webhooks;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private AuthSecretProtector Protector { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<Webhook> _hooks = [];
    private List<Webhook> _filtered = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];

    private Webhook _newHook = NewBlankWebhook();
    private string _newSecret = string.Empty;

    private bool AllSelected => _filtered.Count > 0 && _selectedIds.Count == _filtered.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newHook.Name) && !string.IsNullOrWhiteSpace(_newHook.Url);

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _hooks = await db.Webhooks.AsNoTracking().OrderBy(w => w.Name).ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void OnNewSecretChanged(string secret) => _newSecret = secret;

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<Webhook> query = term.Length == 0
            ? _hooks
            : _hooks.Where(w => w.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                                || w.Url.Contains(term, StringComparison.OrdinalIgnoreCase));

        _filtered = query.OrderBy(w => w.Name).ToList();
        _selectedIds.IntersectWith(_filtered.Select(w => w.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (Webhook hook in _filtered)
            {
                _selectedIds.Add(hook.Id);
            }
        }
    }

    private void ToggleSelect(int hookId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(hookId);
        }
        else
        {
            _selectedIds.Remove(hookId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        List<Webhook> toDelete = await db.Webhooks.Where(w => _selectedIds.Contains(w.Id)).ToListAsync();
        db.Webhooks.RemoveRange(toDelete);
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

        if (await db.Webhooks.AsNoTracking().AnyAsync(w => w.Name == _newHook.Name))
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Ce nom est déjà utilisé."));
            return;
        }

        _newHook.SecretProtected = string.IsNullOrEmpty(_newSecret) ? null : Protector.Protect(_newSecret);

        db.Webhooks.Add(_newHook);
        await db.SaveChangesAsync();

        _newHook = NewBlankWebhook();
        _newSecret = string.Empty;

        await JS.InvokeVoidAsync("glpiNg.hideModal", "newWebhookModal");
        await LoadAsync();
    }

    private static Webhook NewBlankWebhook()
    {
        NotificationEventDefinition first = NotificationEventCatalog.All[0];

        return new Webhook
        {
            Name = string.Empty,
            ItemType = first.ItemType,
            Event = first.EventKey,
            Url = string.Empty,
        };
    }
}
