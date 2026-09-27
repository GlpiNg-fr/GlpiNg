using GlpiNg.Modules.Abstractions.Localization;
﻿using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Webhooks;

/// <summary>
/// Équivalent de la tâche cron GLPI « queuedwebhook » : expédie les <see cref="QueuedWebhook"/>
/// en attente déposées par <see cref="WebhookDispatchService"/>, et purge les livraisons
/// terminées passé l'âge configuré.
///
/// Exécutée au même intervalle global que les autres <see cref="ICronTask"/> : une livraison
/// peut donc attendre jusqu'à cet intervalle. C'est le prix de ne pas faire attendre la requête
/// qui a déclenché l'événement derrière un destinataire lent — un webhook porte une information,
/// pas un ordre synchrone.
/// </summary>
public sealed class QueuedWebhookSenderCronTask(
    GlpiNgDbContext db,
    SettingsCacheService settingsStore,
    WebhookSender sender,
    ILogger<QueuedWebhookSenderCronTask> logger) : ICronTask
{
    /// <summary>Borne le nombre de livraisons par tick, pour ne pas monopoliser le tick cron partagé.</summary>
    private const int BatchSize = 50;

    public string Key => "webhook_queue_sender";

    public string Name => "Envoi des webhooks en file d'attente";

    public string Description =>
        Tr.T("Expédie les appels HTTP sortants en attente, par lots de {0}, et purge les livraisons terminées passé l'âge configuré dans Configuration → Webhooks.", BatchSize);

    public int DefaultFrequencyMinutes => 5;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        WebhookSettings settings = await settingsStore.ReadSectionAsync<WebhookSettings>(
            WebhookDispatchService.SettingsSection, cancellationToken);

        if (!settings.UseWebhooks)
        {
            return;
        }

        await SendPendingAsync(settings, cancellationToken);
        await PurgeAsync(settings, cancellationToken);
    }

    private async Task SendPendingAsync(WebhookSettings settings, CancellationToken cancellationToken)
    {
        List<QueuedWebhook> pending = await db.QueuedWebhooks
            .Where(q => q.Status == QueuedWebhookStatus.Pending)
            .OrderBy(q => q.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        int maxAttempts = Math.Max(1, settings.MaxAttempts);

        foreach (QueuedWebhook delivery in pending)
        {
            WebhookSendResult result = await sender.SendAsync(delivery, settings, cancellationToken);

            delivery.ResponseStatusCode = result.StatusCode;
            delivery.ResponseBody = delivery.SaveResponseBody ? result.ResponseBody : null;

            if (result.Success)
            {
                delivery.Status = QueuedWebhookStatus.Sent;
                delivery.SentAt = DateTime.UtcNow;
                delivery.LastError = null;
                continue;
            }

            delivery.Attempts++;
            delivery.LastError = result.Error;

            if (delivery.Attempts >= maxAttempts)
            {
                delivery.Status = QueuedWebhookStatus.Failed;
                logger.LogWarning("Webhook « {Name} » abandonné après {Attempts} tentative(s) vers {Url} : {Error}",
                    delivery.WebhookName, delivery.Attempts, delivery.Url, result.Error);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PurgeAsync(WebhookSettings settings, CancellationToken cancellationToken)
    {
        if (settings.PurgeAfterDays <= 0)
        {
            return;
        }

        DateTime cutoff = DateTime.UtcNow.AddDays(-settings.PurgeAfterDays);

        // L'âge se compte depuis la fin de la livraison, pas depuis sa mise en file : une
        // livraison restée longtemps en attente — destinataire éteint, webhooks coupés — et qui
        // vient enfin d'aboutir doit rester visible le délai prévu, pas disparaître dans le même
        // tick que son succès. Faute de date de fin (échec définitif), la date de création fait
        // l'affaire : les tentatives s'épuisent en quelques ticks.
        await db.QueuedWebhooks
            .Where(q => q.Status != QueuedWebhookStatus.Pending && (q.SentAt ?? q.CreatedAt) < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
