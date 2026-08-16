using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Notifications.Rules;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<Notification> _rules = [];
    private List<Notification> _filteredRules = [];
    private List<NotificationTemplate> _templates = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private Notification _newRule = NewBlankRule();

    private bool AllSelected => _filteredRules.Count > 0 && _selectedIds.Count == _filteredRules.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newRule.Name) && _newRule.NotificationTemplateId != 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _templates = await db.NotificationTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        _rules = await db.Notifications.AsNoTracking().Include(n => n.Template).OrderBy(n => n.Name).ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<Notification> query = term.Length == 0
            ? _rules
            : _rules.Where(r => r.Name.Contains(term, StringComparison.OrdinalIgnoreCase));

        _filteredRules = query.OrderBy(r => r.Name).ToList();
        _selectedIds.IntersectWith(_filteredRules.Select(r => r.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (Notification rule in _filteredRules)
            {
                _selectedIds.Add(rule.Id);
            }
        }
    }

    private void ToggleSelect(int ruleId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(ruleId);
        }
        else
        {
            _selectedIds.Remove(ruleId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<Notification> toDelete = await db.Notifications
            .Where(r => _selectedIds.Contains(r.Id))
            .ToListAsync();

        db.Notifications.RemoveRange(toDelete);
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

        bool alreadyExists = await db.Notifications.AsNoTracking().AnyAsync(r => r.Name == _newRule.Name);
        if (alreadyExists)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Ce nom est déjà utilisé."));
            return;
        }

        db.Notifications.Add(_newRule);
        await db.SaveChangesAsync();

        _newRule = NewBlankRule();

        await JS.InvokeVoidAsync("glpiNg.hideModal", "newRuleModal");
        await LoadAsync();
    }

    private static Notification NewBlankRule()
    {
        NotificationEventDefinition first = NotificationEventCatalog.All[0];
        return new Notification
        {
            Name = string.Empty,
            ItemType = first.ItemType,
            Event = first.EventKey,
        };
    }
}
