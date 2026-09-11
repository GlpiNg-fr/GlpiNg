namespace GlpiNg.Web.Models.Webhooks;

public enum QueuedWebhookStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}

/// <summary>
/// Livraison d'un webhook (glpi_queuedwebhooks côté GLPI, page /config/webhooks/queue).
/// <see cref="Services.Webhooks.WebhookDispatchService"/> y dépose une ligne au moment de
/// l'événement, corps et en-têtes déjà rendus — donc indépendants d'une modification ultérieure
/// du webhook ; <see cref="Services.Webhooks.QueuedWebhookSenderCronTask"/> les expédie à
/// intervalle régulier.
///
/// Même raison qu'en e-mail de ne pas appeler en synchrone : la requête qui déclenche
/// l'événement (le <c>setStatus</c> d'un agent GLPI-Agent, par exemple) doit répondre vite, et
/// elle n'a pas à attendre un destinataire HTTP lent ou injoignable. Une file donne en prime la
/// reprise sur échec et la trace de ce qui est parti.
/// </summary>
public class QueuedWebhook
{
    public int Id { get; set; }

    /// <summary>Webhook d'origine, pour navigation uniquement (SetNull s'il est supprimé depuis, voir <see cref="WebhookName"/>).</summary>
    public int? WebhookId { get; set; }
    public Webhook? Webhook { get; set; }

    /// <summary>Nom du webhook au moment de l'envoi, conservé même s'il est supprimé depuis.</summary>
    public required string WebhookName { get; set; }

    public required string ItemType { get; set; }
    public int ItemId { get; set; }
    public required string Event { get; set; }

    public required string Url { get; set; }
    public WebhookHttpMethod Method { get; set; }

    /// <summary>Corps déjà rendu. Null pour un GET, qui n'en porte pas.</summary>
    public string? Payload { get; set; }

    /// <summary>En-têtes supplémentaires déjà rendus, sérialisés en JSON (objet nom → valeur).</summary>
    public string? HeadersJson { get; set; }

    /// <summary>Secret chiffré recopié du webhook, pour que la signature reste calculable même
    /// si le webhook est modifié ou supprimé entre la mise en file et l'envoi.</summary>
    public string? SecretProtected { get; set; }

    public bool SaveResponseBody { get; set; }

    public QueuedWebhookStatus Status { get; set; } = QueuedWebhookStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }

    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
