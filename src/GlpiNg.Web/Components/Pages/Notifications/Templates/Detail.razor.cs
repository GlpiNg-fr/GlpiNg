using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Notifications.Templates;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public int TemplateId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private GlpiNgDbContext? _db;
    private NotificationTemplate? _template;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        _db = await DbFactory.CreateDbContextAsync();
        _template = await _db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == TemplateId);
    }

    private async Task SaveAsync()
    {
        if (_db is null || _template is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            _template.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, "Gabarit enregistré."));
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _template is null)
        {
            return;
        }

        bool inUse = await _db.Notifications.AnyAsync(n => n.NotificationTemplateId == _template.Id);
        if (inUse)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Ce gabarit est utilisé par une notification — retirez-le d'abord."));
            return;
        }

        _db.NotificationTemplates.Remove(_template);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/config/notifications/templates");
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
