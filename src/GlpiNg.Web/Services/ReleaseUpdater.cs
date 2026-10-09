using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using GlpiNg.Modules.Abstractions.Localization;
using GlpiNg.Modules.Abstractions.Storage;

namespace GlpiNg.Web.Services;

/// <summary>
/// Mise à jour du programme depuis les releases GitHub (voir <c>.github/workflows/release.yml</c>) :
/// une archive <c>glping-{version}-{rid}.zip</c>, téléchargée par le serveur ou déposée par
/// l'administrateur quand le serveur n'a pas accès au web, est extraite dans la racine du stockage
/// puis installée par-dessus le programme.
///
/// L'installation <b>renomme</b> chaque fichier en place (<see cref="OldSuffix"/>) avant d'y poser
/// le nouveau : Windows refuse d'écraser une DLL chargée mais accepte de la renommer. Le serveur
/// s'arrête ensuite, et c'est son gestionnaire (IIS, systemd, service Windows) qui le relance ; les
/// anciens fichiers sont supprimés au démarrage suivant par <see cref="CleanupPreviousUpdate"/>.
/// </summary>
public sealed class ReleaseUpdater(
    IHttpClientFactory httpFactory,
    IStoragePaths storage,
    IHostApplicationLifetime lifetime,
    ILogger<ReleaseUpdater> logger)
{
    public const string HttpClientName = "GitHubReleases";
    public const string Repository = "GlpiNg-fr/GlpiNg";
    private const string OldSuffix = ".glping-old";

    /// <summary>
    /// Fichiers de la release jamais posés sur un existant : l'écran Système écrit
    /// <c>appsettings.json</c> (adresses d'écoute, racine du stockage — la perdre ferait repartir
    /// sur l'assistant d'installation), et un <c>web.config</c> IIS est souvent retouché sur place.
    /// </summary>
    private static readonly HashSet<string> Preserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "appsettings.json",
        "web.config",
    };

    public sealed record ReleaseInfo(string Tag, string PageUrl, string? AssetUrl);

    /// <summary>Plateforme de cette installation, celle du nom de l'archive à prendre.</summary>
    public static string Rid => RuntimeInformation.RuntimeIdentifier;

    /// <summary>Dans l'image Docker, on met à jour l'image, pas le programme qu'elle contient.</summary>
    public static bool InContainer => Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

    /// <summary>Version installée, sans les métadonnées de compilation (« 1.0.0-RC1 »).</summary>
    public static string CurrentVersion
    {
        get
        {
            string raw = typeof(ReleaseUpdater).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
            return raw.Split('+')[0];
        }
    }

    private string StagedFolder => Path.Combine(storage.Root, "updates", "staged");

    private static string AppHost => OperatingSystem.IsWindows() ? "glping.exe" : "glping";

    /// <summary>Version extraite et prête à être installée, ou <c>null</c>.</summary>
    public string? StagedVersion
    {
        get
        {
            string dll = Path.Combine(StagedFolder, "glping.dll");
            return File.Exists(dll) ? FileVersionInfo.GetVersionInfo(dll).ProductVersion?.Split('+')[0] : null;
        }
    }

    /// <summary>
    /// Dernière release publiée. Une installation en préversion se voit proposer les préversions,
    /// une installation stable seulement les versions stables. Lève une exception quand GitHub est
    /// injoignable — c'est ce qui fait proposer le dépôt manuel de l'archive.
    /// </summary>
    public async Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        bool acceptPrerelease = CurrentVersion.Contains('-');

        HttpClient http = httpFactory.CreateClient(HttpClientName);
        await using Stream body = await http.GetStreamAsync($"https://api.github.com/repos/{Repository}/releases?per_page=20", cancellationToken);
        using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

        foreach (JsonElement release in json.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean()
                || (!acceptPrerelease && release.GetProperty("prerelease").GetBoolean()))
            {
                continue;
            }

            string? assetUrl = release.GetProperty("assets").EnumerateArray()
                .Where(a => a.GetProperty("name").GetString()?.EndsWith($"-{Rid}.zip", StringComparison.OrdinalIgnoreCase) == true)
                .Select(a => a.GetProperty("browser_download_url").GetString())
                .FirstOrDefault();

            return new ReleaseInfo(
                release.GetProperty("tag_name").GetString()!.TrimStart('v'),
                release.GetProperty("html_url").GetString()!,
                assetUrl);
        }

        return null;
    }

    public async Task<string> DownloadAsync(string assetUrl, CancellationToken cancellationToken)
    {
        HttpClient http = httpFactory.CreateClient(HttpClientName);
        await using Stream zip = await http.GetStreamAsync(assetUrl, cancellationToken);
        return await StageAsync(zip, cancellationToken);
    }

    /// <summary>
    /// Extrait une archive de release et vérifie qu'elle est bien une release GlpiNg pour cette
    /// plateforme. Retourne la version extraite.
    /// </summary>
    public async Task<string> StageAsync(Stream zip, CancellationToken cancellationToken)
    {
        string updates = storage.Ensure(Path.Combine(storage.Root, "updates"));
        string zipPath = Path.Combine(updates, "release.zip");

        if (Directory.Exists(StagedFolder))
        {
            Directory.Delete(StagedFolder, recursive: true);
        }

        try
        {
            await using (FileStream file = File.Create(zipPath))
            {
                await zip.CopyToAsync(file, cancellationToken);
            }

            // ExtractToDirectory refuse toute entrée qui sortirait du dossier (« zip slip »).
            ZipFile.ExtractToDirectory(zipPath, StagedFolder);
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException(Tr.T("Ce fichier n'est pas une archive zip."));
        }
        finally
        {
            File.Delete(zipPath);
        }

        if (!File.Exists(Path.Combine(StagedFolder, AppHost)) || StagedVersion is not { } version)
        {
            Directory.Delete(StagedFolder, recursive: true);
            throw new InvalidDataException(Tr.T("Cette archive n'est pas une release GlpiNg pour {0}.", Rid));
        }

        logger.LogInformation("Release GlpiNg {Version} extraite, prête à être installée", version);
        return version;
    }

    /// <summary>
    /// Installe la version extraite puis arrête le serveur. En cas d'échec en cours de route, les
    /// fichiers déjà remplacés sont remis en place : jamais de programme à moitié mis à jour.
    /// </summary>
    public void Apply()
    {
        string target = AppContext.BaseDirectory;
        List<string> replaced = [];

        try
        {
            foreach (string source in Directory.EnumerateFiles(StagedFolder, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(StagedFolder, source);
                string destination = Path.Combine(target, relative);

                if (File.Exists(destination))
                {
                    if (Preserved.Contains(relative))
                    {
                        continue;
                    }

                    File.Move(destination, destination + OldSuffix, overwrite: true);
                }

                // Noté avant la copie : une copie interrompue doit elle aussi être annulée.
                replaced.Add(destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
            }
        }
        catch
        {
            foreach (string destination in replaced)
            {
                File.Delete(destination);
                if (File.Exists(destination + OldSuffix))
                {
                    File.Move(destination + OldSuffix, destination);
                }
            }

            throw;
        }

        string version = StagedVersion ?? "?";
        Directory.Delete(StagedFolder, recursive: true);
        logger.LogWarning("GlpiNg {Version} installé, arrêt du serveur pour redémarrage", version);
        lifetime.StopApplication();
    }

    /// <summary>Supprime les fichiers laissés par la mise à jour précédente, libérés depuis le redémarrage.</summary>
    public static void CleanupPreviousUpdate()
    {
        foreach (string old in Directory.EnumerateFiles(AppContext.BaseDirectory, "*" + OldSuffix, SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(old);
            }
            catch (IOException)
            {
                // Encore verrouillé (ancien processus pas tout à fait terminé) : ce sera pour la prochaine fois.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
