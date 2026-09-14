using AnthoDingo.Setup;
using GlpiNg.Web.Data;
using GlpiNg.Web.Services;
using Microsoft.Extensions.Configuration;

namespace GlpiNg.Console;

public static class ConsoleDbContext
{
    public static GlpiNgDbContext Create(string? basePath = null)
    {
        basePath ??= Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GlpiNg.Web"));

        // appsettings.json d'abord et seul : c'est lui qui porte « Storage:RootPath », dont on a
        // besoin pour savoir où se trouve la configuration propre à l'installation. La chercher
        // dans cette dernière reviendrait à avoir besoin de la racine pour trouver la racine —
        // voir StoragePaths.
        var fileConfig = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(StoragePaths.ResolveLocalSettings(fileConfig[StoragePaths.ConfigurationKey], basePath), optional: true)
            .Build();

        var setupSection = config.GetSection("Setup");
        if (setupSection["IsComplete"] != "True" && setupSection["IsComplete"] != "true")
            throw new InvalidOperationException("L'installation n'est pas terminée. Lancez d'abord GlpiNg.Web et complétez le wizard /setup.");

        var providerString = setupSection["Provider"]
            ?? throw new InvalidOperationException("Le provider de base de données n'est pas configuré (Setup:Provider).");

        if (!Enum.TryParse<DbProvider>(providerString, ignoreCase: true, out var provider))
            throw new InvalidOperationException($"Provider inconnu : {providerString}");

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("La chaîne de connexion DefaultConnection est absente.");

        var fallback = config.GetConnectionString("FallbackConnection");

        return GlpiNgDbContext.Create(provider, connectionString, fallback);
    }
}
