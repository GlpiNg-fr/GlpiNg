namespace GlpiNg.Web.Models.Notifications;

/// <summary>
/// Déclencheur de notification (glpi_notifications côté GLPI) : associe un couple
/// type d'objet/événement (voir <see cref="NotificationEventCatalog"/>) à un
/// <see cref="NotificationTemplate"/> et une liste de <see cref="NotificationRecipient"/>.
/// Contrairement à GLPI, le lien vers le gabarit est direct (une notification = un gabarit) et
/// se limite au mode e-mail : GlpiNg ne propose pas encore de notifications navigateur (pas
/// d'infrastructure de polling/push côté client), donc ce mode n'est volontairement pas exposé
/// plutôt que d'offrir une option qui ne ferait jamais rien.
/// </summary>
public class Notification
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }
    public bool IsActive { get; set; } = true;

    public required string ItemType { get; set; }
    public required string Event { get; set; }

    public int NotificationTemplateId { get; set; }
    public NotificationTemplate? Template { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<NotificationRecipient> Recipients { get; set; } = [];
}
