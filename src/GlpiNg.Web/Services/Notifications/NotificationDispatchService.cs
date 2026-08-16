using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Notifications;

/// <summary>
/// Point d'entrée unique appelé par le code métier quand un événement notifiable survient
/// (voir <see cref="NotificationEventCatalog"/> pour la liste fermée des couples type/événement
/// reconnus, et InventoryImportService/AgentController pour les points d'appel réels). Pour
/// chaque <see cref="Models.Notifications.Notification"/> active correspondante, rend son
/// gabarit (substitution ##cle## à partir des variables fournies par l'appelant — voir
/// <see cref="Render"/>) et dépose une ligne de <see cref="QueuedNotification"/> par
/// destinataire résolu. N'envoie rien elle-même : voir <see cref="QueuedNotificationSenderCronTask"/>,
/// qui vide la file à intervalle régulier — ainsi le code appelant (ex. AgentController.HandleSetStatusAsync,
/// qui doit répondre vite à l'agent) n'attend jamais une connexion SMTP.
/// </summary>
public class NotificationDispatchService(GlpiNgDbContext db, SettingsCacheService settingsStore)
{
    private const string SettingsSection = "NotificationSettings";

    public async Task PublishAsync(string itemType, string eventKey, int itemId, IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken = default)
    {
        NotificationSettings settings = await settingsStore.ReadSectionAsync<NotificationSettings>(SettingsSection, cancellationToken);
        if (!settings.UseNotifications)
        {
            return;
        }

        List<Models.Notifications.Notification> notifications = await db.Notifications
            .Where(n => n.IsActive && n.ItemType == itemType && n.Event == eventKey)
            .Include(n => n.Template)
            .Include(n => n.Recipients).ThenInclude(r => r.User)
            .Include(n => n.Recipients).ThenInclude(r => r.Group).ThenInclude(g => g!.Members).ThenInclude(m => m.User)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return;
        }

        foreach (Models.Notifications.Notification notification in notifications)
        {
            if (notification.Template is null)
            {
                continue;
            }

            string subject = Render(notification.Template.Subject, variables) ?? string.Empty;
            string? text = Render(notification.Template.ContentText, variables);
            string? html = Render(notification.Template.ContentHtml, variables);

            foreach (string email in await ResolveRecipientEmailsAsync(notification, cancellationToken))
            {
                db.QueuedNotifications.Add(new QueuedNotification
                {
                    NotificationId = notification.Id,
                    NotificationName = notification.Name,
                    ItemType = itemType,
                    ItemId = itemId,
                    Subject = subject,
                    BodyText = text,
                    BodyHtml = html,
                    RecipientEmail = email,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<string>> ResolveRecipientEmailsAsync(Models.Notifications.Notification notification, CancellationToken cancellationToken)
    {
        HashSet<string> emails = new(StringComparer.OrdinalIgnoreCase);

        foreach (NotificationRecipient recipient in notification.Recipients)
        {
            switch (recipient.Type)
            {
                case NotificationRecipientType.FixedEmail:
                    if (!string.IsNullOrWhiteSpace(recipient.Email))
                    {
                        emails.Add(recipient.Email);
                    }
                    break;

                case NotificationRecipientType.User:
                    if (recipient.User is { Email: { Length: > 0 } userEmail })
                    {
                        emails.Add(userEmail);
                    }
                    break;

                case NotificationRecipientType.Group:
                    // Le groupe doit être marqué "peut être notifié" (CanBeNotified), à l'image de
                    // GLPI, avant que ses membres reçoivent quoi que ce soit.
                    if (recipient.Group is { CanBeNotified: true })
                    {
                        foreach (Models.GlpiGroupUser member in recipient.Group.Members)
                        {
                            if (member.User is { Email: { Length: > 0 } memberEmail })
                            {
                                emails.Add(memberEmail);
                            }
                        }
                    }
                    break;

                case NotificationRecipientType.AllAdmins:
                    List<string> admins = await db.Users
                        .Where(u => u.IsAdmin && u.IsActive && u.Email != null && u.Email != "")
                        .Select(u => u.Email!)
                        .ToListAsync(cancellationToken);
                    foreach (string admin in admins)
                    {
                        emails.Add(admin);
                    }
                    break;
            }
        }

        return emails.ToList();
    }

    private static string? Render(string? template, IReadOnlyDictionary<string, string?> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        string result = template;
        foreach ((string key, string? value) in variables)
        {
            result = result.Replace($"##{key}##", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
