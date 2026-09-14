using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnthoDingo.Setup;
using GlpiNg.Web.Data;
using GlpiNg.Web.Services;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GlpiNg.Console.Db;

public class InstallCommand : AsyncCommand<InstallCommand.Settings>
{
    private static readonly DbProvider[] AllowedProviders = [DbProvider.SqlServer, DbProvider.MySql, DbProvider.Postgres];

    public class Settings : CommandSettings
    {
        [CommandOption("-p|--provider <PROVIDER>")]
        [Description("Type de base de données : SqlServer, MySql ou Postgres")]
        public string? Provider { get; init; }

        [CommandOption("-c|--connection-string <CS>")]
        [Description("Chaîne de connexion vers la base de données")]
        public string? ConnectionString { get; init; }

        [CommandOption("--web-path <PATH>")]
        [Description("Chemin vers le dossier GlpiNg.Web (y lire Storage:RootPath, où écrire appsettings.local.json)")]
        public string? WebPath { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var webPath = ResolveWebPath(settings.WebPath);

        // Même emplacement que celui où GlpiNg.Web ira le lire : la racine du stockage, dont le
        // chemin se lit dans appsettings.json (voir StoragePaths).
        var localConfigPath = StoragePaths.ResolveLocalSettings(ReadStorageRootPath(webPath), webPath);

        if (File.Exists(localConfigPath))
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(localConfigPath));
            if (root?["Setup"]?["IsComplete"]?.GetValue<string>()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            {
                AnsiConsole.MarkupLine("[red]L'installation est déjà terminée.[/]");
                AnsiConsole.MarkupLine($"Fichier : [dim]{localConfigPath.EscapeMarkup()}[/]");
                return 1;
            }
        }

        var provider = ResolveProvider(settings.Provider);
        var connectionString = settings.ConnectionString
            ?? AnsiConsole.Prompt(new TextPrompt<string>("Chaîne de connexion :"));

        AnsiConsole.MarkupLine($"Provider : [bold]{provider}[/]");
        AnsiConsole.MarkupLine($"Cible    : [dim]{connectionString.EscapeMarkup()}[/]");

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Initialisation de la base de données...", async _ =>
            {
                try
                {
                    await using var db = GlpiNgDbContext.Create(provider, connectionString);

                    if (provider == DbProvider.SqlServer)
                        await db.Database.MigrateAsync();
                    else
                        await db.Database.EnsureCreatedAsync();

                    return (string?)null;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            });

        if (result is not null)
        {
            AnsiConsole.MarkupLine($"[red]Échec de l'initialisation : {result.EscapeMarkup()}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Schéma créé avec succès.[/]");

        Directory.CreateDirectory(Path.GetDirectoryName(localConfigPath)!);
        await WriteLocalConfig(localConfigPath, provider, connectionString);
        AnsiConsole.MarkupLine($"[green]Configuration écrite dans [bold]{localConfigPath.EscapeMarkup()}[/][/]");

        return 0;
    }

    private static DbProvider ResolveProvider(string? raw)
    {
        if (raw is not null)
        {
            if (!Enum.TryParse<DbProvider>(raw, ignoreCase: true, out var parsed) || !AllowedProviders.Contains(parsed))
                throw new InvalidOperationException($"Provider invalide : {raw}. Valeurs acceptées : {string.Join(", ", AllowedProviders)}");
            return parsed;
        }

        return AnsiConsole.Prompt(
            new SelectionPrompt<DbProvider>()
                .Title("Type de base de données :")
                .AddChoices(AllowedProviders));
    }

    private static string ResolveWebPath(string? explicit_path)
    {
        if (explicit_path is not null)
            return Path.GetFullPath(explicit_path);

        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GlpiNg.Web"));
        if (Directory.Exists(candidate))
            return candidate;

        candidate = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "GlpiNg.Web"));
        if (Directory.Exists(candidate))
            return candidate;

        throw new InvalidOperationException(
            "Impossible de trouver le dossier GlpiNg.Web. Utilisez --web-path pour le spécifier.");
    }

    /// <summary>
    /// Racine du stockage telle que configurée dans appsettings.json, ou <c>null</c> pour le
    /// dossier par défaut. Lue à la main plutôt que par ConfigurationBuilder : à l'installation,
    /// le fichier peut ne pas exister encore.
    /// </summary>
    private static string? ReadStorageRootPath(string webPath)
    {
        var settingsPath = Path.Combine(webPath, "appsettings.json");

        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(settingsPath))?["Storage"]?["RootPath"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            // appsettings.json illisible : le dossier par défaut reste le meilleur pari, et
            // l'erreur se manifestera plus clairement au démarrage de GlpiNg.Web.
            return null;
        }
    }

    private static async Task WriteLocalConfig(string path, DbProvider provider, string connectionString)
    {
        var root = new JsonObject
        {
            ["Setup"] = new JsonObject
            {
                ["IsComplete"] = "true",
                ["Provider"] = provider.ToString()
            },
            ["ConnectionStrings"] = new JsonObject
            {
                ["DefaultConnection"] = connectionString
            }
        };

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
}
