using GlpiNg.Modules.Abstractions.Entities;

namespace GlpiNg.Web.Models.Webhooks;

/// <summary>Verbe HTTP employé pour appeler l'URL cible.</summary>
public enum WebhookHttpMethod
{
    Post = 0,
    Put = 1,
    Patch = 2,
    Delete = 3,
    Get = 4,
}

/// <summary>Origine du corps envoyé au destinataire.</summary>
public enum WebhookPayloadMode
{
    /// <summary>Document JSON construit par GlpiNg à partir des balises de l'événement.</summary>
    Default = 0,

    /// <summary>Corps rédigé par l'administrateur, avec substitution des balises ##cle##.</summary>
    Custom = 1,
}

/// <summary>
/// Appel HTTP sortant déclenché par un événement (glpi_webhooks côté GLPI, « Configuration &gt;
/// Webhooks »). Le couple type d'objet/événement vient du même
/// <see cref="Models.Notifications.NotificationEventCatalog"/> que les notifications e-mail :
/// c'est la liste fermée des événements que GlpiNg déclenche réellement, et un webhook posé sur
/// un couple absent de ce catalogue ne partirait jamais.
///
/// Écarts assumés avec GLPI, faute d'équivalent dans GlpiNg :
/// <list type="bullet">
/// <item>Le corps personnalisé substitue des balises <c>##cle##</c> comme les gabarits de
/// notification, là où GLPI interprète du Twig — GlpiNg n'embarque pas de moteur de gabarits.</item>
/// <item>Pas de validation CRA (<c>crc_token</c>) de l'URL à l'enregistrement : le bouton
/// « Tester » envoie une livraison réelle, ce qui répond au même besoin — savoir si l'URL
/// répond — sans imposer au destinataire d'implémenter le protocole de défi de GLPI.</item>
/// </list>
/// </summary>
public class Webhook : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    /// <inheritdoc />
    public bool IsRecursive { get; set; }

    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }
    public bool IsActive { get; set; } = true;

    public required string ItemType { get; set; }
    public required string Event { get; set; }

    public required string Url { get; set; }
    public WebhookHttpMethod Method { get; set; } = WebhookHttpMethod.Post;

    public WebhookPayloadMode PayloadMode { get; set; } = WebhookPayloadMode.Default;

    /// <summary>Corps envoyé quand <see cref="PayloadMode"/> vaut <see cref="WebhookPayloadMode.Custom"/>.</summary>
    public string? CustomPayload { get; set; }

    /// <summary>
    /// Secret partagé, chiffré via AuthSecretProtector avant stockage — jamais en clair en base,
    /// à l'image du mot de passe SMTP de <see cref="Models.Notifications.NotificationSettings"/>.
    /// Quand il est renseigné, chaque livraison porte les en-têtes <c>X-GLPI-signature</c> et
    /// <c>X-GLPI-timestamp</c> (voir WebhookSender), ce qui permet au destinataire de vérifier
    /// que l'appel vient bien de ce serveur.
    /// </summary>
    public string? SecretProtected { get; set; }

    /// <summary>
    /// Conserve le corps de la réponse du destinataire dans la file de livraison. Désactivé par
    /// défaut : une réponse peut être volumineuse, et le code HTTP suffit à savoir si l'appel a
    /// abouti. On l'active le temps de mettre au point une intégration.
    /// </summary>
    public bool SaveResponseBody { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<WebhookHeader> Headers { get; set; } = [];
}

/// <summary>
/// En-tête HTTP supplémentaire envoyé avec ce webhook (authentification par jeton du
/// destinataire, en-tête de routage, ...). La valeur accepte les balises <c>##cle##</c> comme le
/// corps.
/// </summary>
public class WebhookHeader
{
    public int Id { get; set; }

    public int WebhookId { get; set; }
    public Webhook? Webhook { get; set; }

    public required string Name { get; set; }
    public string? Value { get; set; }
}
