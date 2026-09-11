using System.Text.Json;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Webhooks;

/// <summary>
/// Met en file les livraisons dues à un événement. Appelé par
/// <see cref="Notifications.NotificationDispatchService"/>, qui reste le point d'entrée unique du
/// code métier : un webhook écoute exactement les mêmes couples type/événement qu'une
/// notification e-mail, donc les câbler au même endroit évite d'avoir à répercuter chaque
/// nouvel événement à deux endroits — et d'en oublier un.
///
/// N'envoie rien : voir <see cref="QueuedWebhookSenderCronTask"/>.
/// </summary>
public class WebhookDispatchService(GlpiNgDbContext db, SettingsCacheService settingsStore)
{
    public const string SettingsSection = "WebhookSettings";

    public async Task PublishAsync(string itemType, string eventKey, int itemId,
        IReadOnlyDictionary<string, string?> variables, CancellationToken cancellationToken = default)
    {
        WebhookSettings settings = await settingsStore.ReadSectionAsync<WebhookSettings>(SettingsSection, cancellationToken);
        if (!settings.UseWebhooks)
        {
            return;
        }

        List<Webhook> webhooks = await db.Webhooks
            .Where(w => w.IsActive && w.ItemType == itemType && w.Event == eventKey)
            .Include(w => w.Headers)
            .ToListAsync(cancellationToken);

        if (webhooks.Count == 0)
        {
            return;
        }

        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        foreach (Webhook webhook in webhooks)
        {
            db.QueuedWebhooks.Add(BuildDelivery(webhook, itemType, eventKey, itemId, variables, timestamp));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Prépare une livraison sans la mettre en file — utilisé par le bouton « Tester » de la
    /// fiche, qui envoie tout de suite plutôt que d'attendre le prochain tick cron.
    /// </summary>
    public static QueuedWebhook BuildDelivery(Webhook webhook, string itemType, string eventKey, int itemId,
        IReadOnlyDictionary<string, string?> variables, DateTimeOffset timestamp)
    {
        Dictionary<string, string?> tags =
            WebhookPayloadBuilder.BuildTagValues(itemType, eventKey, itemId, variables, timestamp);

        // Un GET ne porte pas de corps : en construire un donnerait une ligne de file trompeuse,
        // puisque rien de ce qu'elle affiche ne partirait réellement.
        string? payload = webhook.Method == WebhookHttpMethod.Get
            ? null
            : webhook.PayloadMode == WebhookPayloadMode.Custom
                ? WebhookPayloadBuilder.Render(webhook.CustomPayload, tags)
                : WebhookPayloadBuilder.BuildDefaultPayload(itemType, eventKey, itemId, webhook.Name, variables, timestamp);

        Dictionary<string, string> headers = [];
        foreach (WebhookHeader header in webhook.Headers)
        {
            if (!string.IsNullOrWhiteSpace(header.Name))
            {
                headers[header.Name] = WebhookPayloadBuilder.Render(header.Value, tags) ?? string.Empty;
            }
        }

        return new QueuedWebhook
        {
            WebhookId = webhook.Id == 0 ? null : webhook.Id,
            WebhookName = webhook.Name,
            ItemType = itemType,
            ItemId = itemId,
            Event = eventKey,
            Url = WebhookPayloadBuilder.Render(webhook.Url, tags) ?? webhook.Url,
            Method = webhook.Method,
            Payload = payload,
            HeadersJson = headers.Count == 0 ? null : JsonSerializer.Serialize(headers),
            SecretProtected = webhook.SecretProtected,
            SaveResponseBody = webhook.SaveResponseBody,
        };
    }
}
