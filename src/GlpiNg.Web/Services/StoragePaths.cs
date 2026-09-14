using GlpiNg.Modules.Abstractions.Storage;

namespace GlpiNg.Web.Services;

/// <summary>
/// Implémentation hôte d'<see cref="IStoragePaths"/>. La racine vient de <c>Storage:RootPath</c>
/// dans <c>appsettings.json</c>, et non de la base : les clés de chiffrement sont configurées au
/// démarrage, avant que l'application n'ait accès à la base — même raison que pour les adresses
/// d'écoute (voir <see cref="AppSettingsFileStore"/>).
///
/// <b>Ni de <see cref="LocalSettingsFileName"/></b>, qui vit désormais <i>dans</i> cette racine :
/// l'y chercher reviendrait à avoir besoin de la racine pour trouver la racine. C'est pourquoi
/// l'écran Système écrit <c>Storage:RootPath</c> dans <c>appsettings.json</c> ; une variable
/// d'environnement (<c>Storage__RootPath</c>) fait aussi l'affaire, elle est lue avant tout
/// fichier.
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

    /// <summary>
    /// Fichier de configuration écrit par l'assistant d'installation. Il vit dans la racine du
    /// stockage, avec les clés et les paquets, plutôt qu'à côté du binaire : tout ce qui est
    /// propre à une installation — et qui doit survivre à une mise à jour du programme — se
    /// trouve alors au même endroit, que l'on sauvegarde ou que l'on monte sur un volume dédié
    /// d'un seul geste.
    /// </summary>
    public const string LocalSettingsFileName = "appsettings.local.json";

    public StoragePaths(IConfiguration configuration, IWebHostEnvironment environment)
        : this(configuration[ConfigurationKey], environment.ContentRootPath)
    {
    }

    public StoragePaths(string? configuredRootPath, string contentRootPath)
    {
        Root = ResolveRoot(configuredRootPath, contentRootPath);
        Keys = Path.Combine(Root, "keys");
        Packages = Path.Combine(Root, "packages");
        Documents = Path.Combine(Root, "documents");
        LocalSettings = Path.Combine(Root, LocalSettingsFileName);
    }

    /// <summary>
    /// Racine du stockage pour une valeur de <see cref="ConfigurationKey"/> donnée. Exposée en
    /// statique parce qu'elle doit être connue <b>avant</b> qu'il n'existe une configuration
    /// complète : c'est de cette racine que l'hôte lit <see cref="LocalSettingsFileName"/>, et la
    /// console la résout de la même façon pour tomber sur le même fichier.
    /// </summary>
    public static string ResolveRoot(string? configuredRootPath, string contentRootPath)
        => string.IsNullOrWhiteSpace(configuredRootPath)
            ? Path.Combine(contentRootPath, DefaultFolderName)
            : Path.GetFullPath(configuredRootPath, contentRootPath);

    /// <summary>Chemin du fichier de configuration local, sans instancier la classe entière.</summary>
    public static string ResolveLocalSettings(string? configuredRootPath, string contentRootPath)
        => Path.Combine(ResolveRoot(configuredRootPath, contentRootPath), LocalSettingsFileName);

    public string Root { get; }

    /// <summary>Fichiers des documents (voir Models/Documents/Document.cs).</summary>
    public string Documents { get; }

    /// <summary>Configuration propre à cette installation — voir <see cref="LocalSettingsFileName"/>.</summary>
    public string LocalSettings { get; }

    public string Keys { get; }

    public string Packages { get; }

    public string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
