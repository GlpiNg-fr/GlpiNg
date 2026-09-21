namespace GlpiNg.Modules.Abstractions.Notifications;

/// <summary>
/// Publication d'un événement notifiable, rendue par l'hôte aux modules.
///
/// Les notifications et les webhooks sont des entités de l'hôte (gabarits, destinataires, file
/// d'attente, expédition par tâche cron), qu'un module ne peut pas manipuler : ce contrat lui donne
/// le seul geste qui le concerne — « ceci vient de se produire » — et laisse l'hôte décider s'il en
/// sort un courriel, un appel HTTP, ou rien du tout.
///
/// Le couple type d'objet / événement doit figurer au catalogue des événements de l'hôte : un
/// couple absent ne déclenchera jamais rien, faute de gabarit possible.
/// </summary>
public interface INotificationPublisher
{
    /// <param name="itemType">Type d'objet au sens du catalogue (« Ticket », ...).</param>
    /// <param name="eventKey">Événement (« new », « solved », ...).</param>
    /// <param name="itemId">Identifiant de l'objet concerné, pour le lien de la notification.</param>
    /// <param name="variables">Valeurs des balises ##clé## du gabarit.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    Task PublishAsync(
        string itemType,
        string eventKey,
        int itemId,
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken = default);
}
