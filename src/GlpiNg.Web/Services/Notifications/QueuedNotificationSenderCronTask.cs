using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Notifications;

/// <summary>
/// Équivalent réduit de la tâche cron GLPI "queuedmail" : expédie par SMTP les
/// <see cref="QueuedNotification"/> en attente, déposées par <see cref="NotificationDispatchService"/>.
/// Exécutée au même intervalle global que les autres <see cref="ICronTask"/> (voir le commentaire
/// de tête de l'interface) — un envoi peut donc attendre jusqu'à cet intervalle, ce qui est
/// acceptable pour des notifications informatives (déploiement/inventaire) et évite de bloquer la
/// requête qui a déclenché l'événement sur une connexion SMTP potentiellement lente.
/// </summary>
public sealed class QueuedNotificationSenderCronTask(
    GlpiNgDbContext db,
    SettingsCacheService settingsStore,
    SmtpMailSender mailSender,
    ILogger<QueuedNotificationSenderCronTask> logger) : ICronTask
{
    private const string SettingsSection = "NotificationSettings";

    /// <summary>Au-delà de ce nombre d'échecs, une notification est marquée définitivement en échec plutôt que retentée indéfiniment.</summary>
    private const int MaxAttempts = 5;

    /// <summary>Borne le nombre de lignes traitées par tick, pour ne pas monopoliser le tick cron partagé si la file s'accumule.</summary>
    private const int BatchSize = 100;

    public string Name => "Envoi des notifications en file d'attente";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        NotificationSettings settings = await settingsStore.ReadSectionAsync<NotificationSettings>(SettingsSection, cancellationToken);
        if (!settings.UseNotifications || !settings.MailingEnabled)
        {
            return;
        }

        List<QueuedNotification> pending = await db.QueuedNotifications
            .Where(q => q.Status == QueuedNotificationStatus.Pending)
            .OrderBy(q => q.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (QueuedNotification queued in pending)
        {
            try
            {
                await mailSender.SendAsync(settings, queued.RecipientEmail, queued.Subject, queued.BodyText, queued.BodyHtml, cancellationToken);
                queued.Status = QueuedNotificationStatus.Sent;
                queued.SentAt = DateTime.UtcNow;
                queued.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                queued.Attempts++;
                queued.LastError = ex.Message;
                if (queued.Attempts >= MaxAttempts)
                {
                    queued.Status = QueuedNotificationStatus.Failed;
                }

                logger.LogWarning(ex, "Échec d'envoi de la notification en file #{Id} vers {Email}", queued.Id, queued.RecipientEmail);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
