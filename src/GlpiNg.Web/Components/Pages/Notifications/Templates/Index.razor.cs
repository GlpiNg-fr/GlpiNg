using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Notifications.Templates;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private List<NotificationTemplate> _templates = [];
    private List<NotificationTemplate> _filteredTemplates = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private NotificationTemplate _newTemplate = NewBlankTemplate();

    private bool AllSelected => _filteredTemplates.Count > 0 && _selectedIds.Count == _filteredTemplates.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newTemplate.Name) && !string.IsNullOrWhiteSpace(_newTemplate.Subject);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _templates = await db.NotificationTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<NotificationTemplate> query = term.Length == 0
            ? _templates
            : _templates.Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase));

        _filteredTemplates = query.OrderBy(t => t.Name).ToList();
        _selectedIds.IntersectWith(_filteredTemplates.Select(t => t.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (NotificationTemplate template in _filteredTemplates)
            {
                _selectedIds.Add(template.Id);
            }
        }
    }

    private void ToggleSelect(int templateId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(templateId);
        }
        else
        {
            _selectedIds.Remove(templateId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        bool inUse = await db.Notifications.AnyAsync(n => _selectedIds.Contains(n.NotificationTemplateId));
        if (inUse)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Au moins un gabarit sélectionné est utilisé par une notification — retirez-le d'abord."));
            return;
        }

        List<NotificationTemplate> toDelete = await db.NotificationTemplates
            .Where(t => _selectedIds.Contains(t.Id))
            .ToListAsync();

        db.NotificationTemplates.RemoveRange(toDelete);
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

        bool alreadyExists = await db.NotificationTemplates.AsNoTracking().AnyAsync(t => t.Name == _newTemplate.Name);
        if (alreadyExists)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Ce nom est déjà utilisé."));
            return;
        }

        db.NotificationTemplates.Add(_newTemplate);
        await db.SaveChangesAsync();

        _newTemplate = NewBlankTemplate();

        await JS.InvokeVoidAsync("glpiNg.hideModal", "newTemplateModal");
        await LoadAsync();
    }

    private static NotificationTemplate NewBlankTemplate() => new()
    {
        Name = string.Empty,
        ItemType = NotificationEventCatalog.All[0].ItemType,
        Subject = string.Empty,
    };
}
