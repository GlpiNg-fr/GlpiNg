using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Web.Models;

namespace GlpiNg.Web.Services;

/// <summary>
/// Lit et modifie appsettings.json pour les réglages exposés par la page de
/// configuration (/config) : adresses d'écoute du serveur et activation de Swagger.
///
/// Écrit directement le fichier plutôt qu'une base : ce sont des réglages de
/// démarrage/hébergement (au même titre que "ConnectionStrings" ou "GlpiImport"
/// déjà dans ce fichier), pas des données métier. Comme appsettings.json est
/// rechargé automatiquement (reloadOnChange), la valeur de "Swagger:Enabled" est
/// reprise en compte sans redémarrage ; "Urls" ne l'est pas (Kestrel ouvre ses
/// sockets au démarrage), ce qui est indiqué sur la page.
/// </summary>
public class AppSettingsFileStore(IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private string SettingsFilePath => Path.Combine(environment.ContentRootPath, "appsettings.json");

    public async Task<ServerSettings> ReadAsync(CancellationToken cancellationToken = default)
    {
        JsonNode root = await ReadRootAsync(cancellationToken);

        string urls = root["Urls"]?.GetValue<string>() ?? "http://0.0.0.0:5000";
        bool swaggerEnabled = root["Swagger"]?["Enabled"]?.GetValue<bool>() ?? false;

        return new ServerSettings
        {
            Urls = urls,
            SwaggerEnabled = swaggerEnabled
        };
    }

    public async Task SaveAsync(ServerSettings settings, CancellationToken cancellationToken = default)
    {
        JsonNode root = await ReadRootAsync(cancellationToken);

        root["Urls"] = settings.Urls;

        JsonObject swaggerSection = root["Swagger"] as JsonObject ?? new JsonObject();
        swaggerSection["Enabled"] = settings.SwaggerEnabled;
        root["Swagger"] = swaggerSection;

        string json = root.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken);
    }

    private async Task<JsonNode> ReadRootAsync(CancellationToken cancellationToken)
    {
        string json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken);
        return JsonNode.Parse(json) ?? new JsonObject();
    }
}
