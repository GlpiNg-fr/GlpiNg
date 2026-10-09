using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using GlpiNg.Modules.Abstractions.Storage;
using GlpiNg.Plugins;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace GlpiNg.Web.Services.Plugins;

/// <summary>Plugin écarté, et la raison à afficher (voir l'onglet « Système »).</summary>
public sealed record SkippedPlugin(string Name, string Reason);

/// <summary>
/// Plugins déposés sous <c>{racine du stockage}/plugins/{Nom}/{Nom}.dll</c> (sortie d'un
/// <c>dotnet publish</c>), chargés au démarrage et chargeables, déchargeables ou rechargeables à
/// chaud (issue #12).
///
/// Chaque plugin vit dans son propre <see cref="PluginLoadContext"/>, chargé depuis une <b>copie</b>
/// de son dossier (<c>plugins/.loaded/</c>) : Windows verrouille une DLL chargée, et sans copie il
/// serait impossible de remplacer le dossier d'un plugin pour le mettre à jour.
///
/// Ce qu'il apporte rejoint l'hôte par des points qui, eux, acceptent le changement :
/// <list type="bullet">
/// <item>services : <see cref="PluginScopes"/>, et pour <c>IMenuProvider</c>, <c>IReportProvider</c>
/// et <c>ICronTask</c> le <c>IEnumerable&lt;T&gt;</c> de l'hôte (voir <see cref="PluginServiceCollectionExtensions"/>) ;</item>
/// <item>pages : <see cref="Assemblies"/>, que <c>Routes.razor</c> relit à chaque <see cref="Changed"/>,
/// et la page attrape-tout <c>/plugins/{*path}</c> pour la saisie d'une URL ;</item>
/// <item>contrôleurs : <see cref="ApplicationPartManager"/>, reconstruit sur signal ;</item>
/// <item>fichiers statiques : <see cref="PluginStaticFileProvider"/>.</item>
/// </list>
///
/// Décharger retire le plugin de tous ces points, mais la mémoire n'est rendue qu'au redémarrage
/// dès qu'une de ses pages a été affichée : Blazor garde chaque type de composant rendu dans des
/// caches statiques qu'il ne vide qu'en hot reload (<see cref="RetainedInMemory"/> le constate).
///
/// Un plugin en défaut est <b>écarté, pas fatal</b> : une DLL invalide, ou prévue pour une autre
/// version de GlpiNg, ne doit pas empêcher le serveur de démarrer.
/// </summary>
public sealed class PluginRegistry
{
    /// <summary>Préfixe imposé aux pages des plugins : c'est lui que sert la page attrape-tout.</summary>
    public const string RoutePrefix = "/plugins/";

    /// <summary>
    /// Les composants d'un plugin reçoivent-ils ses services par <c>@inject</c> ? Oui à partir de
    /// .NET 11 (voir <c>PluginComponentPropertyActivator</c>) ; avant, seulement par leur constructeur.
    /// </summary>
#if NET11_0_OR_GREATER
    public static bool PropertyInjection => true;
#else
    public static bool PropertyInjection => false;
#endif

    /// <summary>Gabarit normalisé de la page attrape-tout de l'hôte, qu'aucun plugin ne peut redéclarer.</summary>
    private static readonly string FallbackRoute = NormalizeRoute("/plugins/{*path}");

    private readonly string _root;
    private readonly IConfiguration _configuration;
    private readonly ApplicationPartManager _parts;
    private readonly PluginActionDescriptorChangeProvider _actions;
    private readonly IServiceProvider _services;
    private readonly ILogger<PluginRegistry> _logger;
    private readonly Version _hostVersion = HostVersion();

    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _maintenance = new(1, 1);
    private readonly HashSet<string> _disabled;
    private readonly Dictionary<string, string> _skipped = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Name, WeakReference Context)> _unloaded = [];
    private volatile PluginInstance[] _active = [];

    public PluginRegistry(
        IStoragePaths storage,
        IConfiguration configuration,
        ApplicationPartManager parts,
        PluginActionDescriptorChangeProvider actions,
        IServiceProvider services,
        ILogger<PluginRegistry> logger)
    {
        // Le dossier des plugins est porté par l'implémentation et non par IStoragePaths, que les
        // modules partagent : eux n'ont rien à y faire.
        _root = ((StoragePaths)storage).Plugins;
        _configuration = configuration;
        _parts = parts;
        _actions = actions;
        _services = services;
        _logger = logger;
        _disabled = File.Exists(DisabledFile)
            ? new(JsonSerializer.Deserialize<string[]>(File.ReadAllText(DisabledFile)) ?? [], StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Levé après chaque changement — chargement, déchargement, installation, suppression,
    /// désactivation — sur le fil de l'appelant : les abonnés Blazor (routes, menu, écran des
    /// plugins) repassent par leur <c>InvokeAsync</c>.
    /// </summary>
    public event Action? Changed;

    /// <summary>Dossier où déposer un plugin.</summary>
    public string Folder => _root;

    private string ShadowRoot => Path.Combine(_root, ".loaded");

    /// <summary>
    /// Plugins désactivés par l'administrateur, à ne pas charger au démarrage. Un fichier dans le
    /// dossier des plugins plutôt qu'une section d'<c>AppSettings</c> : il est lu au démarrage, et une
    /// base injoignable ne doit pas empêcher le serveur de démarrer — même raison que pour <c>Urls</c>.
    /// </summary>
    private string DisabledFile => Path.Combine(_root, "disabled.json");

    /// <summary>Plugins actifs, dans l'ordre de chargement. Instantané : jamais modifié sur place.</summary>
    public IReadOnlyList<PluginInstance> Active => _active;

    /// <summary>Assemblies principales des plugins actifs, pour router leurs pages.</summary>
    public IReadOnlyList<Assembly> Assemblies => [.. _active.Select(p => p.Assembly)];

    public IReadOnlyList<SkippedPlugin> Skipped
    {
        get
        {
            lock (_gate)
            {
                return [.. _skipped.Select(entry => new SkippedPlugin(entry.Key, entry.Value))];
            }
        }
    }

    /// <summary>Plugin qui possède ce type (composant, contrôleur), ou <c>null</c> pour un type de l'hôte.</summary>
    internal static PluginInstance? OwnerOf(Type type)
        => (AssemblyLoadContext.GetLoadContext(type.Assembly) as PluginLoadContext)?.Plugin;

    internal PluginInstance? Find(string name)
        => _active.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool IsDisabled(string name)
    {
        lock (_gate)
        {
            return _disabled.Contains(name);
        }
    }

    /// <summary>Plugins déposés : un dossier <c>{Nom}</c> qui contient <c>{Nom}.dll</c>.</summary>
    public IReadOnlyList<string> Installed()
        => Directory.Exists(_root)
            ? [.. Directory.GetDirectories(_root)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => !name.StartsWith('.') && File.Exists(Path.Combine(_root, name, name + ".dll")))
                .Order(StringComparer.OrdinalIgnoreCase)]
            : [];

    /// <summary>Charge tous les plugins déposés et non désactivés. Appelé une fois, au démarrage.</summary>
    public void LoadAll()
    {
        // Créé plutôt que testé : sans ça le dossier n'existe qu'une fois qu'on y a déposé quelque
        // chose, et il n'y a aucun endroit évident où déposer un premier plugin.
        Directory.CreateDirectory(_root);

        // Copies du lancement précédent, libérées par le redémarrage.
        TryDelete(ShadowRoot);

        foreach (string name in Installed().Where(name => !IsDisabled(name)))
        {
            Load(name);
        }
    }

    /// <summary>Charge un plugin. Retourne la raison de l'échec, ou <c>null</c> s'il est chargé.</summary>
    public string? Load(string name)
    {
        if (!IsValidName(name))
        {
            return "Nom de plugin invalide.";
        }

        lock (_gate)
        {
            if (Find(name) is not null)
            {
                return "Ce plugin est déjà chargé.";
            }

            string folder = Path.Combine(_root, name);
            if (!File.Exists(Path.Combine(folder, name + ".dll")))
            {
                return Skip(name, $"{name}.dll introuvable dans {folder}.");
            }

            string shadow = Path.Combine(ShadowRoot, $"{name}-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
            PluginLoadContext context;

            try
            {
                CopyDirectory(folder, shadow);
                context = new PluginLoadContext(Path.Combine(shadow, name + ".dll"));
            }
            catch (IOException exception)
            {
                return Skip(name, exception.Message);
            }

            PluginInstance? plugin = null;

            try
            {
                plugin = Create(name, context, shadow);
                context.Plugin = plugin;

                // Le signal reconstruit les actions MVC sur-le-champ : un contrôleur invalide lève ici.
                _parts.ApplicationParts.Add(plugin.Part);
                _actions.Signal();
                _active = [.. _active, plugin];
                _skipped.Remove(name);
                _logger.LogInformation("Plugin {Plugin} chargé", name);
            }
            // Code tiers : quoi qu'il lève, le plugin est perdu, le serveur ne doit pas l'être avec lui.
            catch (Exception exception)
            {
                if (plugin is not null && _parts.ApplicationParts.Remove(plugin.Part))
                {
                    _actions.Signal();
                }

                context.Unload();
                TryDelete(shadow);
                return Skip(name, exception is PluginRejectedException ? exception.Message : $"{exception.GetType().Name} : {exception.Message}");
            }
        }

        Changed?.Invoke();
        return null;
    }

    /// <summary>
    /// Retire un plugin : plus de pages, de menus, de tâches ni de contrôleurs. Ses singletons sont
    /// libérés tout de suite ; ses services scoped encore utilisés (un circuit ouvert sur sa page) le
    /// sont à la fin de leur portée. Retourne <c>false</c> s'il n'était pas chargé.
    /// </summary>
    public async Task<bool> UnloadAsync(string name)
    {
        PluginInstance? plugin;

        lock (_gate)
        {
            plugin = Find(name);
            if (plugin is null)
            {
                return false;
            }

            _active = [.. _active.Where(p => p != plugin)];
            _parts.ApplicationParts.Remove(plugin.Part);
            _actions.Signal();
            _unloaded.Add((plugin.Name, new WeakReference(plugin.Context)));
        }

        Changed?.Invoke();

        await plugin.DisposeAsync();
        plugin.Context.Unload();
        _logger.LogInformation("Plugin {Plugin} déchargé", name);

        return true;
    }

    /// <summary>Recharge un plugin depuis son dossier — après l'avoir remplacé par une nouvelle version.</summary>
    public async Task<string?> ReloadAsync(string name)
    {
        await UnloadAsync(name);
        return Load(name);
    }

    /// <summary>Réactive un plugin et le charge. Retourne la raison d'un échec de chargement.</summary>
    public string? Enable(string name)
    {
        SetDisabled(name, false);
        return Load(name);
    }

    /// <summary>Désactive un plugin : déchargé tout de suite, et plus chargé aux démarrages suivants.</summary>
    public async Task DisableAsync(string name)
    {
        SetDisabled(name, true);

        if (!await UnloadAsync(name))
        {
            // Pas chargé (écarté, ou jamais chargé) : l'état affiché change quand même.
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Installe ou met à jour un plugin depuis l'archive zip de son <c>dotnet publish</c> — le
    /// dossier de publication lui-même, ou son contenu. Le nom du plugin est celui de son
    /// <c>.deps.json</c>. Une version déjà chargée est déchargée puis remplacée ; un plugin
    /// désactivé est remplacé sans être chargé. Retourne le nom installé, et la raison d'un échec.
    /// </summary>
    public async Task<(string? Name, string? Error)> InstallAsync(Stream zip, CancellationToken cancellationToken = default)
    {
        // Un seul dossier de travail : les installations passent une par une.
        string staging = Path.Combine(_root, ".upload");

        await _maintenance.WaitAsync(cancellationToken);
        try
        {
            TryDelete(staging);
            Directory.CreateDirectory(staging);
            string archive = Path.Combine(staging, "plugin.zip");
            await using (FileStream file = File.Create(archive))
            {
                await zip.CopyToAsync(file, cancellationToken);
            }

            // ExtractToDirectory refuse toute entrée qui sortirait du dossier (« zip slip »).
            string extracted = Path.Combine(staging, "files");
            ZipFile.ExtractToDirectory(archive, extracted);
            if (Directory.GetFiles(extracted).Length == 0 && Directory.GetDirectories(extracted) is [var single])
            {
                extracted = single;
            }

            string[] deps = Directory.GetFiles(extracted, "*.deps.json");
            if (deps.Length != 1)
            {
                return (null, "L'archive doit contenir la sortie d'un dotnet publish : un seul fichier .deps.json à sa racine.");
            }

            string name = Path.GetFileName(deps[0])[..^".deps.json".Length];
            if (!IsValidName(name) || !File.Exists(Path.Combine(extracted, name + ".dll")))
            {
                return (null, $"{name}.dll absent de l'archive.");
            }

            // Chargé depuis une copie : son dossier n'est pas verrouillé et peut être remplacé.
            await UnloadAsync(name);
            string folder = Path.Combine(_root, name);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            Directory.Move(extracted, folder);
            _logger.LogInformation("Plugin {Plugin} installé", name);

            string? error = IsDisabled(name) ? null : Load(name);
            Changed?.Invoke();
            return (name, error);
        }
        catch (InvalidDataException)
        {
            return (null, "Ce fichier n'est pas une archive zip.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, exception.Message);
        }
        finally
        {
            TryDelete(staging);
            _maintenance.Release();
        }
    }

    /// <summary>Décharge un plugin et supprime son dossier. Retourne la raison d'un échec.</summary>
    public async Task<string?> DeleteAsync(string name)
    {
        if (!IsValidName(name))
        {
            return "Nom de plugin invalide.";
        }

        await _maintenance.WaitAsync();
        try
        {
            await UnloadAsync(name);

            string folder = Path.Combine(_root, name);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            lock (_gate)
            {
                _skipped.Remove(name);
            }

            SetDisabled(name, false);
            _logger.LogInformation("Plugin {Plugin} supprimé", name);
            Changed?.Invoke();
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
        finally
        {
            _maintenance.Release();
        }
    }

    private void SetDisabled(string name, bool disabled)
    {
        lock (_gate)
        {
            if (disabled ? _disabled.Add(name) : _disabled.Remove(name))
            {
                Directory.CreateDirectory(_root);
                File.WriteAllText(DisabledFile, JsonSerializer.Serialize(_disabled.Order(StringComparer.OrdinalIgnoreCase)));
            }
        }
    }

    private static bool IsValidName(string name)
        => name.Length > 0 && Path.GetFileName(name) == name && !name.StartsWith('.')
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>
    /// Plugins déchargés dont la mémoire n'a pas pu être rendue, après un ramasse-miettes forcé : en
    /// pratique, ceux dont une page a été affichée. Elle le sera au prochain redémarrage.
    /// </summary>
    public IReadOnlyList<string> RetainedInMemory()
    {
        for (int i = 0; i < 2; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        lock (_gate)
        {
            _unloaded.RemoveAll(entry => !entry.Context.IsAlive);
            return [.. _unloaded.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase)];
        }
    }

    private PluginInstance Create(string name, PluginLoadContext context, string folder)
    {
        Assembly assembly = context.LoadFromAssemblyPath(Path.Combine(folder, name + ".dll"));
        Type[] exported = assembly.GetExportedTypes();

        Type[] entryPoints = exported
            .Where(t => typeof(IGlpiNgPlugin).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .ToArray();
        if (entryPoints.Length != 1)
        {
            throw new PluginRejectedException(
                $"{entryPoints.Length} classe(s) publique(s) implémentent {nameof(IGlpiNgPlugin)}, une seule est attendue.");
        }

        IGlpiNgPlugin entryPoint = (IGlpiNgPlugin)Activator.CreateInstance(entryPoints[0])!;

        // La compatibilité est vérifiée avant ConfigureServices : un plugin hors intervalle ne doit
        // rien exécuter du tout.
        if (Incompatibility(entryPoint, _hostVersion) is { } incompatibility)
        {
            throw new PluginRejectedException(incompatibility);
        }

        string[] routes = [.. exported
            .Where(t => typeof(IComponent).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>(inherit: false))
            .Select(route => NormalizeRoute(route.Template))];
        CheckRoutes(routes);

        ServiceCollection declared = [];
        entryPoint.ConfigureServices(declared, _configuration);

        (List<ServiceDescriptor> services, List<string> warnings) = Filter(declared, context);
#if !NET11_0_OR_GREATER
        warnings.AddRange(PropertyInjectionWarnings(exported, services));
#endif

        foreach (string warning in warnings)
        {
            _logger.LogWarning("Plugin {Plugin} : {Warning}", name, warning);
        }

        return new PluginInstance(name, context, assembly, folder, routes, services, warnings, _services);
    }

    /// <summary>
    /// Les pages d'un plugin vivent sous <see cref="RoutePrefix"/>, que sert la page attrape-tout,
    /// et ne doivent pas reprendre une route existante : une route en double fait lever le
    /// <c>Router</c> pour tous les utilisateurs, pas seulement sur cette page.
    /// </summary>
    private void CheckRoutes(string[] routes)
    {
        if (routes.FirstOrDefault(route => !route.StartsWith(RoutePrefix, StringComparison.Ordinal)) is { } outside)
        {
            throw new PluginRejectedException($"La page « {outside} » doit commencer par {RoutePrefix}.");
        }

        HashSet<string> taken = [FallbackRoute, .. _active.SelectMany(p => p.Routes)];
        if (routes.FirstOrDefault(route => !taken.Add(route)) is { } duplicate)
        {
            throw new PluginRejectedException($"La page « {duplicate} » existe déjà (hôte ou autre plugin).");
        }
    }

    /// <summary>
    /// Gabarit réduit à ce qui le rend ambigu pour le <c>Router</c> : segments littéraux en
    /// minuscules, paramètres réduits à leur contrainte (« /plugins/x/{id:int} » → « /plugins/x/{int} »).
    /// </summary>
    internal static string NormalizeRoute(string template)
        => "/" + string.Join('/', template.Trim('/').Split('/').Select(segment =>
            segment.StartsWith('{')
                ? "{" + (segment.StartsWith("{*") ? "*" : string.Empty)
                    + (segment.Split(':', 2) is [_, var constraint] ? constraint.TrimEnd('}', '?') : string.Empty) + "}"
                : segment.ToLowerInvariant()));

    /// <summary>
    /// Garde les services dont l'implémentation vient du plugin. Le reste — ce que
    /// <c>AddControllers</c>, <c>AddHttpClient</c>... ajoutent du framework — existe déjà chez l'hôte,
    /// et c'est celui-là que le plugin reçoit ; ce qui ne peut pas fonctionner est signalé.
    /// </summary>
    private static (List<ServiceDescriptor> Services, List<string> Warnings) Filter(IServiceCollection declared, AssemblyLoadContext context)
    {
        List<ServiceDescriptor> services = [];
        List<string> warnings = [];
        int framework = 0;

        foreach (ServiceDescriptor descriptor in declared)
        {
            Type? implementation = descriptor.IsKeyedService
                ? descriptor.KeyedImplementationType
                    ?? descriptor.KeyedImplementationInstance?.GetType()
                    ?? descriptor.KeyedImplementationFactory?.Method.DeclaringType
                : descriptor.ImplementationType
                    ?? descriptor.ImplementationInstance?.GetType()
                    ?? descriptor.ImplementationFactory?.Method.DeclaringType;
            if (implementation is null || AssemblyLoadContext.GetLoadContext(implementation.Assembly) != context)
            {
                framework++;
                continue;
            }

            string? unsupported = descriptor switch
            {
                { IsKeyedService: true } => "service keyed",
                { ServiceType.IsGenericTypeDefinition: true } => "générique ouvert",
                _ when descriptor.ServiceType == typeof(IHostedService) => "IHostedService (utilisez ICronTask)",
                _ => null,
            };
            if (unsupported is not null)
            {
                warnings.Add($"{implementation.Name} ignoré : {unsupported} non pris en charge pour un plugin.");
                continue;
            }

            services.Add(descriptor);
        }

        if (framework > 0)
        {
            warnings.Add($"{framework} enregistrement(s) de services du framework ignoré(s) : le plugin reçoit ceux de l'hôte.");
        }

        return (services, warnings);
    }

#if !NET11_0_OR_GREATER
    /// <summary>
    /// Avant .NET 11, Blazor remplit les propriétés <c>[Inject]</c> depuis le conteneur de l'hôte,
    /// qui ignore les services du plugin : la page lèverait à l'affichage. Signalé dès le chargement.
    /// </summary>
    private static IEnumerable<string> PropertyInjectionWarnings(Type[] exported, List<ServiceDescriptor> services)
    {
        HashSet<Type> own = [.. services.Select(d => d.ServiceType)];

        foreach (Type component in exported.Where(t => typeof(IComponent).IsAssignableFrom(t)))
        {
            for (Type? type = component; type is not null && type != typeof(object); type = type.BaseType)
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (own.Contains(property.PropertyType) && property.IsDefined(typeof(InjectAttribute)))
                    {
                        yield return $"{component.Name}.{property.Name} : un service du plugin ne s'injecte pas par @inject avant .NET 11, passez par le constructeur.";
                    }
                }
            }
        }
    }
#endif

    private string Skip(string name, string reason)
    {
        _skipped[name] = reason;
        _logger.LogWarning("Plugin {Plugin} écarté : {Reason}", name, reason);
        return reason;
    }

    private static void CopyDirectory(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Encore verrouillé (plugin déchargé mais pas encore collecté) : nettoyé au prochain démarrage.
        }
    }

    /// <summary>
    /// Raison pour laquelle le plugin ne convient pas à cette version de GlpiNg, ou <c>null</c>
    /// s'il convient. Une borne illisible est traitée comme une borne absente : c'est une erreur
    /// de déclaration du plugin, pas une raison de le refuser.
    /// </summary>
    private static string? Incompatibility(IGlpiNgPlugin plugin, Version host)
    {
        if (Parse(plugin.MinimumHostVersion) is { } min && host < min)
        {
            return $"Demande GlpiNg {plugin.MinimumHostVersion} ou plus récent ; ce serveur est en {host}.";
        }

        if (Parse(plugin.MaximumHostVersion) is { } max && host > max)
        {
            return $"Éprouvé jusqu'à GlpiNg {plugin.MaximumHostVersion} ; ce serveur est en {host}.";
        }

        return null;
    }

    /// <summary>Version de l'hôte, réduite à ses nombres : « 1.2.0-RC1+abc1234 » donne 1.2.0.</summary>
    private static Version HostVersion()
    {
        Assembly host = typeof(PluginRegistry).Assembly;
        string raw = host.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? host.GetName().Version?.ToString()
            ?? string.Empty;

        return Parse(raw) ?? new Version(0, 0);
    }

    /// <summary>
    /// Partie numérique d'une version, suffixe de pré-version et métadonnées de compilation
    /// retirés. Comparer « 1.2.0-RC1 » à « 1.2.0 » par les règles SemVer rendrait la pré-version
    /// antérieure, et un plugin annoncé pour 1.2.0 refuserait de se charger sur la RC qui
    /// l'accompagne — ici les deux valent 1.2.0.
    /// </summary>
    private static Version? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string numeric = value.Trim().Split('+')[0].Split('-')[0];

        return Version.TryParse(numeric, out Version? parsed) ? parsed : null;
    }

    /// <summary>Refus motivé d'un plugin, dont le message est montré tel quel.</summary>
    private sealed class PluginRejectedException(string reason) : Exception(reason);
}
