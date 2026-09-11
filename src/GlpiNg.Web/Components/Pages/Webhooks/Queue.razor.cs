using System.Text.Json;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Models.Webhooks;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Webhooks;

public partial class Queue : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    private List<QueuedWebhook> _items = [];
    private QueuedWebhook? _inspected;

    private int SentOrFailedCount => _items.Count(i => i.Status != QueuedWebhookStatus.Pending);

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _items = await db.QueuedWebhooks
            .AsNoTracking()
            .OrderByDescending(q => q.CreatedAt)
            .Take(500)
            .ToListAsync();

        // La ligne inspectée peut avoir disparu (purge, suppression) ou changé de statut : on la
        // reprend dans la liste rechargée plutôt que de garder un instantané périmé à l'écran.
        _inspected = _inspected is null ? null : _items.FirstOrDefault(i => i.Id == _inspected.Id);
    }

    private void Inspect(QueuedWebhook item) => _inspected = _inspected?.Id == item.Id ? null : item;

    private async Task RetryAsync(int id)
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        QueuedWebhook? item = await db.QueuedWebhooks.FirstOrDefaultAsync(q => q.Id == id);
        if (item is null)
        {
            return;
        }

        item.Status = QueuedWebhookStatus.Pending;
        item.LastError = null;

        // Le compteur repart de zéro : sans ça une livraison déjà au plafond des tentatives
        // retomberait en échec au premier essai, et le bouton n'aurait servi à rien.
        item.Attempts = 0;

        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task DeleteAsync(int id)
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        QueuedWebhook? item = await db.QueuedWebhooks.FirstOrDefaultAsync(q => q.Id == id);
        if (item is null)
        {
            return;
        }

        db.QueuedWebhooks.Remove(item);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task PurgeSentAndFailedAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        await db.QueuedWebhooks
            .Where(q => q.Status != QueuedWebhookStatus.Pending)
            .ExecuteDeleteAsync();

        await LoadAsync();
    }

    private static string EventLabel(QueuedWebhook item) =>
        $"{NotificationEventCatalog.ItemTypeLabel(item.ItemType)} — {NotificationEventCatalog.EventLabel(item.ItemType, item.Event)}";

    /// <summary>Réaffiche le JSON des en-têtes en une ligne par en-tête, plus lisible qu'un objet brut.</summary>
    private static string FormatHeaders(string headersJson)
    {
        try
        {
            Dictionary<string, string> headers =
                JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [];

            return string.Join('\n', headers.Select(header => $"{header.Key}: {header.Value}"));
        }
        catch (JsonException)
        {
            return headersJson;
        }
    }

    private static string StatusLabel(QueuedWebhookStatus status) => status switch
    {
        QueuedWebhookStatus.Pending => "En attente",
        QueuedWebhookStatus.Sent => "Envoyé",
        QueuedWebhookStatus.Failed => "Échec",
        _ => status.ToString()
    };

    private static string StatusBadgeClass(QueuedWebhookStatus status) => status switch
    {
        QueuedWebhookStatus.Pending => "bg-secondary",
        QueuedWebhookStatus.Sent => "bg-success",
        QueuedWebhookStatus.Failed => "bg-danger",
        _ => "bg-secondary"
    };
}
