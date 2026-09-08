using GlpiNg.Modules.Abstractions.Storage;

namespace GlpiNg.Web.Services;

/// <summary>
/// Implémentation hôte d'<see cref="IStoragePaths"/>. La racine vient de <c>Storage:RootPath</c>
/// dans <c>appsettings.json</c>, et non de la base : les clés de chiffrement sont configurées au
/// démarrage, avant que l'application n'ait accès à la base — même raison que pour les adresses
/// d'écoute (voir <see cref="AppSettingsFileStore"/>).
///
/// Un chemin relatif est résolu depuis la racine du programme, pour qu'une valeur comme
/// <c>data</c> désigne la même chose quel que soit le répertoire de travail du service.
/// </summary>
public sealed class StoragePaths : IStoragePaths
{
    /// <summary>Clé de configuration, également écrite par l'écran Système.</summary>
    public const string ConfigurationKey = "Storage:RootPath";

    /// <summary>Dossier utilisé quand aucun chemin n'est configuré.</summary>
    public const string DefaultFolderName = "data";

    public StoragePaths(IConfiguration configuration, IWebHostEnvironment environment)
    {
        string? configured = configuration[ConfigurationKey];

        Root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, DefaultFolderName)
            : Path.GetFullPath(configured, environment.ContentRootPath);

        Keys = Path.Combine(Root, "keys");
        Packages = Path.Combine(Root, "packages");
    }

    public string Root { get; }

    public string Keys { get; }

    public string Packages { get; }

    public string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
