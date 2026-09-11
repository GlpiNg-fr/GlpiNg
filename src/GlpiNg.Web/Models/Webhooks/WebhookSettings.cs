namespace GlpiNg.Web.Models.Webhooks;

/// <summary>
/// Réglages généraux des webhooks (page /config/webhooks, onglet « Configuration »). Section JSON
/// stockée via <see cref="Services.SettingsCacheService"/> comme les autres pages /config.
///
/// GLPI n'a pas d'interrupteur global équivalent, mais un destinataire en panne se coupe mieux
/// d'un seul geste que webhook par webhook, et le délai d'attente n'a pas à être réglé sur
/// chacun.
/// </summary>
public class WebhookSettings
{
    /// <summary>Interrupteur général : à faux, plus rien n'est mis en file ni expédié.</summary>
    public bool UseWebhooks { get; set; } = true;

    /// <summary>Délai d'attente d'un appel sortant, en secondes.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Au-delà de ce nombre d'échecs, une livraison est marquée en échec définitif
    /// plutôt que retentée à chaque tick jusqu'à la fin des temps.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Purge automatique des livraisons terminées (envoyées ou en échec définitif) au-delà de cet
    /// âge, en jours. Zéro désactive la purge — la file se garde alors entièrement, ce qui n'est
    /// tenable que sur un serveur de faible volume.
    /// </summary>
    public int PurgeAfterDays { get; set; } = 30;
}
