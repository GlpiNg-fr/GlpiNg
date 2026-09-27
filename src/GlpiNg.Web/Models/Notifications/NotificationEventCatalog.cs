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
    public const string NetworkTaskJob = "Deployment.NetworkTaskJob";
    public const string DiscoveredNetworkDevice = "Deployment.DiscoveredNetworkDevice";
    public const string WakeOnLanTaskJob = "Deployment.WakeOnLanTaskJob";
    public const string FieldUnicity = "Config.FieldUnicity";

    /// <summary>
    /// Ticket d'assistance. Sans préfixe de module, contrairement aux autres : le module Assistance
    /// publie ses événements avec <c>Abstractions/Items/ItemTypes.Ticket</c>, le nom que GLPI donne
    /// déjà à ce type et que portent aussi ses documents et ses notes. Un préfixe ici obligerait à
    /// tenir deux noms pour le même objet, et le couple ne correspondrait plus à la publication.
    /// </summary>
    public const string AssistanceTicket = GlpiNg.Modules.Abstractions.Items.ItemTypes.Ticket;

    /// <inheritdoc cref="AssistanceTicket"/>
    public const string AssistanceProblem = GlpiNg.Modules.Abstractions.Items.ItemTypes.Problem;

    /// <inheritdoc cref="AssistanceTicket"/>
    public const string AssistanceChange = GlpiNg.Modules.Abstractions.Items.ItemTypes.Change;

    public const string EventNew = "new";
    public const string EventSuccess = "success";
    public const string EventError = "error";
    public const string EventDuplicate = "duplicate";

    /// <summary>Un niveau d'escalade de niveau de service s'est appliqué à un ticket.</summary>
    public const string EventEscalation = "escalation";

    /// <summary>Une approbation est demandée sur un changement (clé « validation » de GLPI).</summary>
    public const string EventValidation = "validation";

    /// <summary>Un approbateur a répondu (clé « validation_answer » de GLPI).</summary>
    public const string EventValidationAnswer = "validation_answer";

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

        new(NetworkTaskJob, "Tâche réseau (Découverte/Inventaire SNMP)", EventSuccess, "Tâche réseau réussie",
        [
            new("job.agent", "Agent exécutant"),
            new("job.status", "Statut"),
            new("job.url", "Lien vers les tâches réseau"),
        ]),

        new(NetworkTaskJob, "Tâche réseau (Découverte/Inventaire SNMP)", EventError, "Tâche réseau en échec",
        [
            new("job.agent", "Agent exécutant"),
            new("job.status", "Statut"),
            new("job.log", "Dernière ligne du journal"),
            new("job.url", "Lien vers les tâches réseau"),
        ]),

        new(DiscoveredNetworkDevice, "Actif non géré (Découverte réseau)", EventNew, "Nouvel équipement réseau découvert",
        [
            new("device.ip", "Adresse IP"),
            new("device.mac", "Adresse MAC"),
            new("device.hostname", "Nom d'hôte"),
            new("device.url", "Lien vers les actifs non gérés"),
        ]),

        new(WakeOnLanTaskJob, "Réveil réseau (WakeOnLan)", EventSuccess, "Réveil réseau réussi",
        [
            new("job.agent", "Agent relais"),
            new("job.status", "Statut"),
            new("job.url", "Lien vers les tâches de réveil réseau"),
        ]),

        new(WakeOnLanTaskJob, "Réveil réseau (WakeOnLan)", EventError, "Réveil réseau en échec",
        [
            new("job.agent", "Agent relais"),
            new("job.status", "Statut"),
            new("job.log", "Dernière ligne du journal"),
            new("job.url", "Lien vers les tâches de réveil réseau"),
        ]),

        // Publié par FieldUnicityService pour un critère dont la case « Envoyer une notification »
        // est cochée, que la création soit refusée ou seulement signalée : "unicity.refused" dit
        // laquelle des deux, pour qu'un même gabarit puisse servir aux deux usages.
        new(FieldUnicity, "Unicité des champs", EventDuplicate, "Doublon détecté",
        [
            new("unicity.criterion", "Nom du critère"),
            new("unicity.itemtype", "Type d'objet"),
            new("unicity.fields", "Champs en doublon"),
            new("unicity.values", "Valeurs en doublon"),
            new("unicity.refused", "Création refusée (oui/non)"),
        ]),

        // Publié à l'ouverture d'un ticket (module Assistance, Tickets/Index.CreateAsync), par le
        // contrat INotificationPublisher.
        new(AssistanceTicket, "Ticket (Assistance)", EventNew, "Nouveau ticket ouvert",
        [
            new("ticket.id", "Numéro du ticket"),
            new("ticket.title", "Titre"),
            new("ticket.type", "Type (incident/demande)"),
            new("ticket.status", "Statut"),
            new("ticket.priority", "Priorité"),
            new("ticket.requester", "Demandeur"),
            new("ticket.category", "Catégorie"),
            new("ticket.url", "Lien vers la fiche"),
        ]),

        // Publié par la tâche cron d'escalade des niveaux de service, quand un niveau portant
        // l'action « Notifier » s'applique à un ticket.
        new(AssistanceTicket, "Ticket (Assistance)", EventEscalation, "Escalade d'un niveau de service",
        [
            new("ticket.id", "Numéro du ticket"),
            new("escalation.name", "Niveau d'escalade déclenché"),
            new("ticket.url", "Lien vers la fiche"),
        ]),

        // Publié à l'ouverture d'un problème (module Assistance, Problems/Index.CreateAsync).
        new(AssistanceProblem, "Problème (Assistance)", EventNew, "Nouveau problème ouvert",
        [
            new("problem.id", "Numéro du problème"),
            new("problem.title", "Titre"),
            new("problem.status", "Statut"),
            new("problem.priority", "Priorité"),
            new("problem.author", "Rédacteur"),
            new("problem.category", "Catégorie"),
            new("problem.url", "Lien vers la fiche"),
        ]),

        // Publié à l'ouverture d'un changement (module Assistance, Changes/Index.CreateAsync).
        new(AssistanceChange, "Changement (Assistance)", EventNew, "Nouveau changement ouvert",
        [
            new("change.id", "Numéro du changement"),
            new("change.title", "Titre"),
            new("change.status", "Statut"),
            new("change.priority", "Priorité"),
            new("change.author", "Rédacteur"),
            new("change.category", "Catégorie"),
            new("change.url", "Lien vers la fiche"),
        ]),

        // Publié quand une approbation est demandée sur un changement (Changes/Detail).
        new(AssistanceChange, "Changement (Assistance)", EventValidation, "Approbation demandée",
        [
            new("change.id", "Numéro du changement"),
            new("change.title", "Titre"),
            new("validation.validator", "Approbateur"),
            new("validation.requester", "Demandeur de l'approbation"),
            new("validation.comment", "Commentaire de la demande"),
            new("change.url", "Lien vers la fiche"),
        ]),

        // Publié quand un approbateur répond (accord ou refus).
        new(AssistanceChange, "Changement (Assistance)", EventValidationAnswer, "Réponse à une approbation",
        [
            new("change.id", "Numéro du changement"),
            new("change.title", "Titre"),
            new("validation.validator", "Approbateur"),
            new("validation.status", "Réponse (Accepté / Refusé)"),
            new("validation.answer", "Commentaire de l'approbateur"),
            new("change.url", "Lien vers la fiche"),
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
