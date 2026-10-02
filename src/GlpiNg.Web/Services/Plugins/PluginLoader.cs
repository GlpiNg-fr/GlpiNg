using System.Reflection;
using System.Runtime.Loader;
using GlpiNg.Plugins;

namespace GlpiNg.Web.Services.Plugins;

/// <summary>Plugin écarté au démarrage, et la raison à afficher (voir l'onglet « Système »).</summary>
public sealed record SkippedPlugin(string Name, string Reason);

/// <summary>
/// Assemblies des plugins chargés au démarrage : Routes.razor et <c>MapRazorComponents</c> les
/// ajoutent aux assemblies des modules pour router leurs pages <c>@page</c>.
///
/// <c>Skipped</c> porte ceux qui ont été écartés : un plugin qui disparaît sans un mot est
/// indiagnosticable, et c'est le cas le plus probable après une mise à jour de GlpiNg.
/// </summary>
public sealed record LoadedPlugins(IReadOnlyList<Assembly> Assemblies, IReadOnlyList<SkippedPlugin> Skipped)
{
    public LoadedPlugins(IReadOnlyList<Assembly> assemblies) : this(assemblies, [])
    {
    }
}

/// <summary>
/// Charge les plugins déposés sous <c>{racine du stockage}/plugins/{Nom}/{Nom}.dll</c> (sortie
/// d'un <c>dotnet publish</c>) et appelle leur <see cref="IGlpiNgPlugin.ConfigureServices"/>.
///
/// Chargement dans le contexte par défaut : les assemblies que l'hôte possède déjà
/// (Abstractions, ASP.NET Core, EF Core) y sont résolues en premier, si bien qu'un plugin voit les
/// mêmes types que l'hôte — sans quoi son <c>IMenuProvider</c> ne serait pas celui que l'hôte
/// cherche. Ses dépendances privées sont trouvées dans son dossier par <c>Resolving</c>.
///
/// Un plugin en défaut est <b>écarté, pas fatal</b> : une DLL invalide, ou prévue pour une autre
/// version de GlpiNg, ne doit pas empêcher le serveur de démarrer.
/// </summary>
public static class PluginLoader
{
    // ponytail: contexte unique, pas de déchargement ni d'isolation des versions entre plugins —
    // un AssemblyLoadContext par plugin si la distribution/installation à chaud l'exige (issue #12).
    public static LoadedPlugins Load(string pluginsRoot, IServiceCollection services, IConfiguration configuration)
    {
        // Créé plutôt que testé : sans ça le dossier n'existe qu'une fois qu'on y a déposé quelque
        // chose, et il n'y a aucun endroit évident où déposer un premier plugin.
        Directory.CreateDirectory(pluginsRoot);

        Version host = HostVersion();
        List<Assembly> assemblies = [];
        List<SkippedPlugin> skipped = [];

        foreach (string folder in Directory.GetDirectories(pluginsRoot).Order(StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(folder);
            string path = Path.Combine(folder, name + ".dll");
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                AssemblyDependencyResolver resolver = new(path);
                AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
                    resolver.ResolveAssemblyToPath(assemblyName) is { } dependency
                        ? context.LoadFromAssemblyPath(dependency)
                        : null;

                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                Type[] entryPoints = assembly.GetExportedTypes()
                    .Where(t => typeof(IGlpiNgPlugin).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
                    .ToArray();

                if (entryPoints.Length != 1)
                {
                    skipped.Add(new SkippedPlugin(name,
                        $"{entryPoints.Length} classe(s) publique(s) implémentent {nameof(IGlpiNgPlugin)}, une seule est attendue."));
                    continue;
                }

                IGlpiNgPlugin plugin = (IGlpiNgPlugin)Activator.CreateInstance(entryPoints[0])!;

                // La compatibilité est vérifiée avant ConfigureServices : un plugin hors intervalle
                // ne doit rien enregistrer du tout, pas même à moitié.
                if (Incompatibility(plugin, host) is { } reason)
                {
                    skipped.Add(new SkippedPlugin(name, reason));
                    continue;
                }

                plugin.ConfigureServices(services, configuration);
                assemblies.Add(assembly);
            }
            catch (Exception exception) when (exception is BadImageFormatException or FileLoadException
                                                  or ReflectionTypeLoadException or TypeLoadException
                                                  or MissingMethodException)
            {
                // DLL illisible, compilée pour une autre plateforme, ou dont un type ne se résout
                // pas : le plugin est perdu, le serveur ne doit pas l'être avec lui.
                skipped.Add(new SkippedPlugin(name, exception.Message));
            }
        }

        return new LoadedPlugins(assemblies, skipped);
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
        Assembly host = typeof(PluginLoader).Assembly;
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
}
