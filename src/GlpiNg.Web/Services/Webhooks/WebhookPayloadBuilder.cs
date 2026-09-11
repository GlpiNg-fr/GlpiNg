using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Models.Webhooks;

namespace GlpiNg.Web.Services.Webhooks;

/// <summary>
/// Construit le corps envoyé à un destinataire, et substitue les balises <c>##cle##</c> dans les
/// valeurs rédigées par l'administrateur (corps personnalisé, valeurs d'en-têtes).
///
/// Même convention de balises que les gabarits de notification, pour qu'un administrateur n'ait
/// pas deux syntaxes à connaître selon qu'il envoie un e-mail ou un appel HTTP : les clés
/// disponibles sont celles que <see cref="NotificationEventCatalog"/> déclare pour l'événement.
/// </summary>
public static class WebhookPayloadBuilder
{
    /// <summary>Balises fournies en plus de celles du catalogue, quel que soit l'événement.</summary>
    public const string EventTag = "event";
    public const string ItemTypeTag = "itemtype";
    public const string ItemIdTag = "items_id";
    public const string TimestampTag = "timestamp";

    /// <summary>
    /// Corps par défaut : les métadonnées de l'événement à plat, et les valeurs du catalogue
    /// regroupées sous <c>data</c>.
    ///
    /// Les clés du catalogue sont pointées (<c>computer.name</c>) : elles sont laissées telles
    /// quelles plutôt que dépliées en objets imbriqués, pour que le JSON reçu se lise exactement
    /// comme la liste de balises affichée dans l'interface. Un destinataire qui préfère une autre
    /// forme passe en corps personnalisé.
    /// </summary>
    public static string BuildDefaultPayload(string itemType, string eventKey, int itemId, string webhookName,
        IReadOnlyDictionary<string, string?> variables, DateTimeOffset timestamp)
    {
        JsonObject data = [];
        foreach ((string key, string? value) in variables)
        {
            data[key] = value;
        }

        JsonObject payload = new()
        {
            ["webhook"] = webhookName,
            [EventTag] = eventKey,
            ["event_label"] = NotificationEventCatalog.EventLabel(itemType, eventKey),
            [ItemTypeTag] = itemType,
            ["itemtype_label"] = NotificationEventCatalog.ItemTypeLabel(itemType),
            [ItemIdTag] = itemId,
            [TimestampTag] = timestamp.ToString("O"),
            ["data"] = data,
        };

        return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Valeurs disponibles à la substitution : celles fournies par l'appelant, plus les
    /// métadonnées de l'événement. Les métadonnées sont ajoutées en dernier et écrasent une clé
    /// de même nom : une balise documentée comme toujours disponible doit l'être vraiment.
    /// </summary>
    public static Dictionary<string, string?> BuildTagValues(string itemType, string eventKey, int itemId,
        IReadOnlyDictionary<string, string?> variables, DateTimeOffset timestamp)
    {
        Dictionary<string, string?> tags = new(variables, StringComparer.OrdinalIgnoreCase)
        {
            [EventTag] = eventKey,
            [ItemTypeTag] = itemType,
            [ItemIdTag] = itemId.ToString(),
            [TimestampTag] = timestamp.ToString("O"),
        };

        return tags;
    }

    /// <summary>Substitue les <c>##cle##</c> présentes dans <paramref name="template"/>.</summary>
    public static string? Render(string? template, IReadOnlyDictionary<string, string?> tags)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        string result = template;
        foreach ((string key, string? value) in tags)
        {
            result = result.Replace($"##{key}##", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Balises proposées dans l'interface pour un couple type/événement : celles du catalogue,
    /// suivies des métadonnées communes.
    /// </summary>
    public static IReadOnlyList<NotificationTag> TagsFor(string itemType, string eventKey)
    {
        List<NotificationTag> tags = [.. NotificationEventCatalog.Find(itemType, eventKey)?.Tags ?? []];

        tags.Add(new NotificationTag(EventTag, "Clé de l'événement"));
        tags.Add(new NotificationTag(ItemTypeTag, "Type d'objet"));
        tags.Add(new NotificationTag(ItemIdTag, "Identifiant de l'objet"));
        tags.Add(new NotificationTag(TimestampTag, "Horodatage de l'événement (ISO 8601)"));

        return tags;
    }
}
