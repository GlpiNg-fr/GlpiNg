using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Web.Models;

namespace GlpiNg.Web.Services;

/// <summary>
/// Lit et modifie appsettings.json pour les seuls réglages qui doivent rester dans un fichier
/// plutôt qu'en base (voir <see cref="Services.SettingsCacheService"/> pour tout le reste,
/// onglet "Configuration générale" y compris) : adresses d'écoute du serveur, activation de
/// Swagger, et racine du stockage des fichiers.
///
/// Ces deux réglages restent dans appsettings.json car ce sont des réglages de
/// démarrage/hébergement plutôt que des données métier : "Urls" est lu par Kestrel à
/// l'ouverture des sockets, avant que l'application n'ait accès à la base, et nécessite de
/// toute façon un redémarrage pour être pris en compte ; l'activation de Swagger s'appuie sur
/// IOptionsMonitor&lt;SwaggerOptions&gt; + le rechargement automatique du fichier
/// (reloadOnChange) pour basculer à chaud, un mécanisme propre au fichier de config qui n'a
/// pas d'équivalent direct côté base. La racine du stockage relève de la même contrainte que
/// "Urls" : les clés de chiffrement sont configurées au démarrage, avant tout accès à la base.
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
        string? storageRootPath = root["Storage"]?["RootPath"]?.GetValue<string>();

        return new ServerSettings
        {
            Urls = urls,
            SwaggerEnabled = swaggerEnabled,
            StorageRootPath = storageRootPath
        };
    }

    public async Task SaveAsync(ServerSettings settings, CancellationToken cancellationToken = default)
    {
        JsonNode root = await ReadRootAsync(cancellationToken);

        root["Urls"] = settings.Urls;

        JsonObject swaggerSection = root["Swagger"] as JsonObject ?? new JsonObject();
        swaggerSection["Enabled"] = settings.SwaggerEnabled;
        root["Swagger"] = swaggerSection;

        // Un chemin vide est écrit comme null plutôt que comme chaîne vide : la clé absente et la
        // clé vide doivent signifier la même chose — « emplacement par défaut ».
        JsonObject storageSection = root["Storage"] as JsonObject ?? new JsonObject();
        storageSection["RootPath"] = string.IsNullOrWhiteSpace(settings.StorageRootPath)
            ? null
            : settings.StorageRootPath.Trim();
        root["Storage"] = storageSection;

        string json = root.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken);
    }

    private async Task<JsonNode> ReadRootAsync(CancellationToken cancellationToken)
    {
        string json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken);
        return JsonNode.Parse(json) ?? new JsonObject();
    }
}
