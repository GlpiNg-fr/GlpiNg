using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Notifications;

public partial class Queue : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    private List<QueuedNotification> _items = [];

    private int SentOrFailedCount => _items.Count(i => i.Status != QueuedNotificationStatus.Pending);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _items = await db.QueuedNotifications.AsNoTracking().OrderByDescending(q => q.CreatedAt).Take(500).ToListAsync();
    }

    private async Task RetryAsync(int id)
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        QueuedNotification? item = await db.QueuedNotifications.FirstOrDefaultAsync(q => q.Id == id);
        if (item is null)
        {
            return;
        }

        item.Status = QueuedNotificationStatus.Pending;
        item.LastError = null;
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task DeleteAsync(int id)
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        QueuedNotification? item = await db.QueuedNotifications.FirstOrDefaultAsync(q => q.Id == id);
        if (item is null)
        {
            return;
        }

        db.QueuedNotifications.Remove(item);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task PurgeSentAndFailedAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        await db.QueuedNotifications
            .Where(q => q.Status != QueuedNotificationStatus.Pending)
            .ExecuteDeleteAsync();

        await LoadAsync();
    }

    private static string StatusLabel(QueuedNotificationStatus status) => status switch
    {
        QueuedNotificationStatus.Pending => "En attente",
        QueuedNotificationStatus.Sent => "Envoyé",
        QueuedNotificationStatus.Failed => "Échec",
        _ => status.ToString()
    };

    private static string StatusBadgeClass(QueuedNotificationStatus status) => status switch
    {
        QueuedNotificationStatus.Pending => "bg-secondary",
        QueuedNotificationStatus.Sent => "bg-success",
        QueuedNotificationStatus.Failed => "bg-danger",
        _ => "bg-secondary"
    };
}
