namespace GlpiNg.Web.Models.Notifications;

/// <summary>Balise substituable dans un gabarit (##Key##), voir <see cref="NotificationEventCatalog"/>.</summary>
public sealed record NotificationTag(string Key, string Label);

/// <summary>
/// Couple type d'objet / événement déclencheur, avec les balises ##...## que
/// <see cref="Services.Notifications.NotificationDispatchService"/> fournit en valeur pour cet
/// événement précis. Équivalent réduit du couple itemtype/event de GLPI (Notification.itemtype
/// + Notification.event), mais borné aux seuls événements réellement déclenchés par GlpiNg — voir
/// le commentaire de tête de <see cref="NotificationEventCatalog"/>.
/// </summary>
public sealed record NotificationEventDefinition(string ItemType, string ItemTypeLabel, string EventKey, string EventLabel, IReadOnlyList<NotificationTag> Tags);

/// <summary>
/// Catalogue statique des couples type d'objet/événement pouvant déclencher une notification.
/// GLPI expose des dizaines d'événements liés aux Tickets/Problèmes/Changements/Réservations/
/// Contrats — autant de modules qui n'existent pas (encore) dans GlpiNg. Ce catalogue ne liste
/// donc que les événements réellement câblés côté serveur (voir les appels à
/// NotificationDispatchService.PublishAsync dans InventoryImportService et AgentController) :
/// une <see cref="Models.Notification"/> créée pour un couple absent d'ici ne se déclenchera
/// jamais, donc les pages d'administration (Templates/Notifications) restreignent leurs
/// sélecteurs à ce catalogue plutôt que de laisser saisir un couple arbitraire.
/// </summary>
public static class NotificationEventCatalog
{
    public const string InventoryComputer = "Inventory.Computer";
    public const string DeploymentAgent = "Deployment.Agent";
    public const string DeploymentJob = "Deployment.Job";

    public const string EventNew = "new";
    public const string EventSuccess = "success";
    public const string EventError = "error";

    public static readonly IReadOnlyList<NotificationEventDefinition> All =
    [
        new(InventoryComputer, "Ordinateur (Inventaire)", EventNew, "Nouvel ordinateur découvert",
        [
            new("computer.name", "Nom de l'ordinateur"),
            new("computer.serial", "Numéro de série"),
            new("computer.manufacturer", "Fabricant"),
            new("computer.model", "Modèle"),
            new("computer.os", "Système d'exploitation"),
            new("computer.url", "Lien vers la fiche"),
        ]),

        new(DeploymentAgent, "Agent GLPI (Déploiement)", EventNew, "Nouvel agent enregistré",
        [
            new("agent.name", "Nom de l'agent"),
            new("agent.uuid", "Identifiant (UUID)"),
            new("agent.version", "Version de l'agent"),
            new("agent.url", "Lien vers la fiche"),
        ]),

        new(DeploymentJob, "Déploiement de paquet", EventSuccess, "Déploiement réussi",
        [
            new("job.package", "Nom du paquet"),
            new("job.agent", "Agent cible"),
            new("job.computer", "Ordinateur cible"),
            new("job.status", "Statut"),
            new("job.url", "Lien vers la supervision"),
        ]),

        new(DeploymentJob, "Déploiement de paquet", EventError, "Déploiement en échec",
        [
            new("job.package", "Nom du paquet"),
            new("job.agent", "Agent cible"),
            new("job.computer", "Ordinateur cible"),
            new("job.status", "Statut"),
            new("job.log", "Dernière ligne du journal"),
            new("job.url", "Lien vers la supervision"),
        ]),
    ];

    public static IEnumerable<(string ItemType, string Label)> ItemTypes =>
        All.Select(e => (e.ItemType, e.ItemTypeLabel)).Distinct();

    public static IEnumerable<NotificationEventDefinition> EventsFor(string itemType) =>
        All.Where(e => e.ItemType == itemType);

    public static NotificationEventDefinition? Find(string itemType, string eventKey) =>
        All.FirstOrDefault(e => e.ItemType == itemType && e.EventKey == eventKey);

    public static string EventLabel(string itemType, string eventKey) =>
        Find(itemType, eventKey)?.EventLabel ?? eventKey;

    public static string ItemTypeLabel(string itemType) =>
        All.FirstOrDefault(e => e.ItemType == itemType)?.ItemTypeLabel ?? itemType;
}
