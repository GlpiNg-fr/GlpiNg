using GlpiNg.Modules.Abstractions.Notifications;

namespace GlpiNg.Web.Services.Notifications;

/// <summary>
/// Rend aux modules la publication d'événements notifiables — voir
/// <see cref="INotificationPublisher"/>. Simple passe-plat vers
/// <see cref="NotificationDispatchService"/> : tout ce qui décide (réglages, gabarits,
/// destinataires, file d'attente) reste chez l'hôte, et le module n'en connaît rien.
/// </summary>
public sealed class ModuleNotificationPublisher(NotificationDispatchService dispatch) : INotificationPublisher
{
    public Task PublishAsync(
        string itemType,
        string eventKey,
        int itemId,
        IReadOnlyDictionary<string, string?> variables,
        CancellationToken cancellationToken = default)
        => dispatch.PublishAsync(itemType, eventKey, itemId, variables, cancellationToken);
}
