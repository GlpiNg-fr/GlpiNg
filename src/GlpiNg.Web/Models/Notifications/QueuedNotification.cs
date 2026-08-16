namespace GlpiNg.Web.Models.Notifications;

public enum QueuedNotificationStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}

/// <summary>
/// File d'attente des notifications (glpi_queuednotifications côté GLPI, page /config/notifications/queue,
/// équivalent de "Administration &gt; File d'attente des notifications"). <see cref="NotificationDispatchService"/>
/// y insère une ligne par destinataire résolu au moment de l'événement (rendu du gabarit déjà fait, donc
/// indépendant d'une modification ultérieure du gabarit) ; <see cref="QueuedNotificationSenderCronTask"/>
/// les expédie via SMTP à intervalle régulier plutôt que d'envoyer en synchrone pendant la requête qui a
/// déclenché l'événement (ex. setStatus d'un agent GLPI, qui doit rester rapide).
/// </summary>
public class QueuedNotification
{
    public int Id { get; set; }

    /// <summary>Notification d'origine, pour navigation uniquement (SetNull si supprimée depuis, voir <see cref="NotificationName"/>).</summary>
    public int? NotificationId { get; set; }
    public Notification? Notification { get; set; }

    /// <summary>Nom de la notification au moment de l'envoi, conservé même si celle-ci est supprimée depuis.</summary>
    public required string NotificationName { get; set; }

    public required string ItemType { get; set; }
    public int ItemId { get; set; }

    public required string Subject { get; set; }
    public string? BodyText { get; set; }
    public string? BodyHtml { get; set; }

    public required string RecipientEmail { get; set; }

    public QueuedNotificationStatus Status { get; set; } = QueuedNotificationStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
