using System.Reflection;
using System.Runtime.Loader;
using GlpiNg.Plugins;

namespace GlpiNg.Web.Services.Plugins;

/// <summary>
/// Assemblies des plugins chargés au démarrage : Routes.razor et <c>MapRazorComponents</c> les
/// ajoutent aux assemblies des modules pour router leurs pages <c>@page</c>.
/// </summary>
public sealed record LoadedPlugins(IReadOnlyList<Assembly> Assemblies);

/// <summary>
/// Charge les plugins déposés sous <c>{racine du stockage}/plugins/{Nom}/{Nom}.dll</c> (sortie
/// d'un <c>dotnet publish</c>) et appelle leur <see cref="IGlpiNgPlugin.ConfigureServices"/>.
///
/// Chargement dans le contexte par défaut : les assemblies que l'hôte possède déjà
/// (Abstractions, ASP.NET Core, EF Core) y sont résolues en premier, si bien qu'un plugin voit les
/// mêmes types que l'hôte — sans quoi son <c>IMenuProvider</c> ne serait pas celui que l'hôte
/// cherche. Ses dépendances privées sont trouvées dans son dossier par <c>Resolving</c>.
/// </summary>
public static class PluginLoader
{
    // ponytail: contexte unique, pas de déchargement ni d'isolation des versions entre plugins —
    // un AssemblyLoadContext par plugin si la distribution/installation à chaud l'exige.
    public static LoadedPlugins Load(string pluginsRoot, IServiceCollection services, IConfiguration configuration)
    {
        if (!Directory.Exists(pluginsRoot))
        {
            return new LoadedPlugins([]);
        }

        List<Assembly> assemblies = [];
        foreach (string folder in Directory.GetDirectories(pluginsRoot).Order(StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(folder);
            string path = Path.Combine(folder, name + ".dll");
            if (!File.Exists(path))
            {
                continue;
            }

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
                throw new InvalidOperationException(
                    $"Plugin « {name} » : {entryPoints.Length} classe(s) publique(s) implémentent {nameof(IGlpiNgPlugin)}, une seule est attendue ({path}).");
            }

            ((IGlpiNgPlugin)Activator.CreateInstance(entryPoints[0])!).ConfigureServices(services, configuration);
            assemblies.Add(assembly);
        }

        return new LoadedPlugins(assemblies);
    }
}
