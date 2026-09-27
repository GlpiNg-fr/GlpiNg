using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlpiNg.Web.Models.Webhooks;

namespace GlpiNg.Web.Services.Webhooks;

/// <summary>Ce qu'on a appris d'une livraison : le code HTTP obtenu, le corps si on le garde, l'erreur sinon.</summary>
public sealed record WebhookSendResult(bool Success, int? StatusCode, string? ResponseBody, string? Error);

/// <summary>
/// Exécute une livraison. Partagé par <see cref="QueuedWebhookSenderCronTask"/> (qui vide la
/// file) et par le bouton « Tester » de la fiche, pour que le test emprunte exactement le même
/// chemin que la vraie livraison — un test qui passe par un autre code ne prouve pas grand-chose.
/// </summary>
public class WebhookSender(IHttpClientFactory httpClientFactory, AuthSecretProtector protector,
    ILogger<WebhookSender> logger)
{
    /// <summary>Nom du client nommé, configuré dans Program.cs (proxy désactivé, redirections coupées).</summary>
    public const string HttpClientName = "Webhook";

    /// <summary>Au-delà, le corps de réponse conservé est tronqué : la file est un journal, pas un cache.</summary>
    private const int MaxStoredResponseLength = 4000;

    public async Task<WebhookSendResult> SendAsync(QueuedWebhook delivery, WebhookSettings settings,
        CancellationToken cancellationToken = default)
    {
        HttpClient client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 1, 300));

        try
        {
            using HttpRequestMessage request = BuildRequest(delivery);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

            string? body = null;
            if (delivery.SaveResponseBody)
            {
                string raw = await response.Content.ReadAsStringAsync(cancellationToken);
                body = raw.Length > MaxStoredResponseLength ? raw[..MaxStoredResponseLength] + "…" : raw;
            }

            int status = (int)response.StatusCode;

            // Le succès se juge sur le code HTTP, pas sur l'absence d'exception : un 500 est une
            // requête parfaitement aboutie côté réseau, et une livraison qu'il faut retenter.
            return response.IsSuccessStatusCode
                ? new WebhookSendResult(true, status, body, null)
                : new WebhookSendResult(false, status, body, $"Le destinataire a répondu {status} {response.ReasonPhrase}.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WebhookSendResult(false, null, null, $"Délai d'attente dépassé ({settings.TimeoutSeconds} s).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Échec de livraison du webhook « {Name} » vers {Url}", delivery.WebhookName, delivery.Url);
            return new WebhookSendResult(false, null, null, Describe(ex));
        }
    }

    private HttpRequestMessage BuildRequest(QueuedWebhook delivery)
    {
        HttpRequestMessage request = new(ToHttpMethod(delivery.Method), delivery.Url);

        string body = delivery.Payload ?? string.Empty;

        if (delivery.Method != WebhookHttpMethod.Get)
        {
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        // Signature au format GLPI (voir Webhook::getSignature) : HMAC-SHA256 en hexadécimal du
        // corps concaténé à l'horodatage, pour que le destinataire puisse à la fois authentifier
        // l'appel et rejeter un rejeu. Les deux en-têtes vont ensemble — une signature sans
        // l'horodatage qu'elle couvre est invérifiable.
        string? secret = protector.Unprotect(delivery.SecretProtected);
        if (!string.IsNullOrEmpty(secret))
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            request.Headers.TryAddWithoutValidation("X-GLPI-signature", Sign(body + timestamp, secret));
            request.Headers.TryAddWithoutValidation("X-GLPI-timestamp", timestamp);
        }

        foreach ((string name, string value) in ParseHeaders(delivery.HeadersJson))
        {
            // TryAddWithoutValidation, et repli sur les en-têtes de contenu : Content-Type et
            // consorts sont refusés sur la requête elle-même par HttpClient.
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return request;
    }

    /// <summary>HMAC-SHA256 en hexadécimal minuscule, comme <c>hash_hmac('sha256', ...)</c> côté PHP.</summary>
    public static string Sign(string data, string secret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)));

    public static Dictionary<string, string> ParseHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [];
        }
        catch (JsonException)
        {
            // Une ligne de file illisible ne doit pas empêcher l'appel : on part sans ses
            // en-têtes supplémentaires plutôt que de ne pas partir du tout.
            return [];
        }
    }

    public static HttpMethod ToHttpMethod(WebhookHttpMethod method) => method switch
    {
        WebhookHttpMethod.Put => HttpMethod.Put,
        WebhookHttpMethod.Patch => HttpMethod.Patch,
        WebhookHttpMethod.Delete => HttpMethod.Delete,
        WebhookHttpMethod.Get => HttpMethod.Get,
        _ => HttpMethod.Post,
    };

    /// <summary>Déroule la chaîne d'exceptions : sans ça un échec réseau se résume souvent à
    /// « Une erreur s'est produite lors de l'envoi de la requête », qui n'apprend rien.</summary>
    private static string Describe(Exception exception)
    {
        List<string> parts = [];
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (!parts.Contains(current.Message))
            {
                parts.Add(current.Message);
            }
        }

        return string.Join(" — ", parts);
    }
}
