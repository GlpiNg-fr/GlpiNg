namespace GlpiNg.Web.Models.Notifications;

public enum SmtpEncryptionMode
{
    None = 0,
    StartTls = 1,
    Ssl = 2,
}

/// <summary>
/// Réglages généraux des notifications (page /config/notifications, onglet "Configuration") :
/// équivalent de setup.notification.php côté GLPI (activation générale + envoi mail) plus la
/// configuration du serveur SMTP sortant, absente de GlpiNg jusqu'ici (voir le commentaire
/// "Nécessite un moteur de notifications... qui n'existe pas encore" sur les fiches Entité/Groupe).
/// Section JSON stockée via <see cref="Services.SettingsCacheService"/> comme les autres pages
/// /config ; <see cref="SmtpPasswordProtected"/> est chiffré via AuthSecretProtector avant d'être
/// inclus dans ce blob, à l'image du mot de passe LDAP de AuthLdapServer.
/// </summary>
public class NotificationSettings
{
    public bool UseNotifications { get; set; } = true;

    /// <summary>Active réellement l'envoi (mise en file + tâche cron d'expédition) — équivalent de "notifications_mailing" côté GLPI.</summary>
    public bool MailingEnabled { get; set; }

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public SmtpEncryptionMode Encryption { get; set; } = SmtpEncryptionMode.StartTls;

    public bool RequiresAuthentication { get; set; } = true;
    public string? SmtpUsername { get; set; }

    /// <summary>Mot de passe SMTP chiffré (AuthSecretProtector.Protect) — jamais en clair, y compris dans la table AppSettings.</summary>
    public string? SmtpPasswordProtected { get; set; }

    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "GlpiNg";
    public string? ReplyToEmail { get; set; }
}
