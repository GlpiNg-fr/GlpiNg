using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Web.Models;

namespace GlpiNg.Web.Services;

/// <summary>
/// Lit et modifie appsettings.json pour les réglages exposés par la page de
/// configuration (/config) : adresses d'écoute du serveur, activation de Swagger
/// (onglet "Système") et réglages généraux type GLPI — URL de l'application, textes
/// d'aide, etc. (onglet "Configuration générale").
///
/// Écrit directement le fichier plutôt qu'une base : ce sont des réglages de
/// démarrage/hébergement (au même titre que "ConnectionStrings" ou "GlpiImport"
/// déjà dans ce fichier), pas des données métier. Comme appsettings.json est
/// rechargé automatiquement (reloadOnChange), la plupart des valeurs sont reprises
/// en compte sans redémarrage ; "Urls" fait exception (Kestrel ouvre ses sockets au
/// démarrage), ce qui est indiqué sur la page.
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

    public async Task<GeneralSettings> ReadGeneralAsync(CancellationToken cancellationToken = default)
    {
        JsonNode root = await ReadRootAsync(cancellationToken);
        JsonNode? section = root["GeneralSettings"];

        return new GeneralSettings
        {
            ApplicationUrl = section?["ApplicationUrl"]?.GetValue<string>() ?? "http://localhost",
            LoginPageMessage = section?["LoginPageMessage"]?.GetValue<string>(),
            SimplifiedInterfaceHelpLink = section?["SimplifiedInterfaceHelpLink"]?.GetValue<string>(),
            StandardInterfaceHelpLink = section?["StandardInterfaceHelpLink"]?.GetValue<string>(),
            DefaultDecimalsCount = section?["DefaultDecimalsCount"]?.GetValue<int>() ?? 2,
            AllowAnonymousFaqAccess = section?["AllowAnonymousFaqAccess"]?.GetValue<bool>() ?? false,
            AllowAnonymousFileImports = section?["AllowAnonymousFileImports"]?.GetValue<bool>() ?? false,
            DefaultDropdownListLimit = section?["DefaultDropdownListLimit"]?.GetValue<int>() ?? 100,
            SearchEngineDisableThreshold = section?["SearchEngineDisableThreshold"]?.GetValue<int>() ?? 50
        };
    }

    public async Task SaveGeneralAsync(GeneralSettings settings, CancellationToken cancellationToken = default)
    {
        JsonNode root = await ReadRootAsync(cancellationToken);

        JsonObject section = root["GeneralSettings"] as JsonObject ?? new JsonObject();
        section["ApplicationUrl"] = settings.ApplicationUrl;
        section["LoginPageMessage"] = settings.LoginPageMessage;
        section["SimplifiedInterfaceHelpLink"] = settings.SimplifiedInterfaceHelpLink;
        section["StandardInterfaceHelpLink"] = settings.StandardInterfaceHelpLink;
        section["DefaultDecimalsCount"] = settings.DefaultDecimalsCount;
        section["AllowAnonymousFaqAccess"] = settings.AllowAnonymousFaqAccess;
        section["AllowAnonymousFileImports"] = settings.AllowAnonymousFileImports;
        section["DefaultDropdownListLimit"] = settings.DefaultDropdownListLimit;
        section["SearchEngineDisableThreshold"] = settings.SearchEngineDisableThreshold;
        root["GeneralSettings"] = section;

        string json = root.ToJsonString(WriteOptions);
        await File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken);
    }

    private async Task<JsonNode> ReadRootAsync(CancellationToken cancellationToken)
    {
        string json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken);
        return JsonNode.Parse(json) ?? new JsonObject();
    }
}
