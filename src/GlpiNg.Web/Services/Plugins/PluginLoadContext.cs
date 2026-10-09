using System.Reflection;
using System.Runtime.Loader;

namespace GlpiNg.Web.Services.Plugins;

/// <summary>
/// Contexte de chargement propre à un plugin, <b>collectible</b> pour pouvoir être déchargé.
///
/// Tout ce que l'hôte possède déjà — framework, ASP.NET Core, EF Core, Abstractions, le SDK, les
/// modules — reste résolu dans le contexte par défaut (<c>Load</c> renvoie <c>null</c>) : le plugin
/// voit alors les mêmes types que l'hôte, sans quoi son <c>IMenuProvider</c> ne serait pas celui
/// que l'hôte cherche. Ses dépendances privées, elles, sont chargées ici depuis son dossier : deux
/// plugins peuvent embarquer deux versions d'une même bibliothèque.
/// </summary>
internal sealed class PluginLoadContext(string mainAssemblyPath)
    : AssemblyLoadContext(Path.GetFileNameWithoutExtension(mainAssemblyPath), isCollectible: true)
{
    /// <summary>
    /// Assemblies de l'hôte : la liste que le lanceur .NET a construite depuis son
    /// <c>.deps.json</c> et le framework partagé — exactement ce que le contexte par défaut sait charger.
    /// </summary>
    private static readonly HashSet<string> HostAssemblies =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

    /// <summary>Plugin chargé dans ce contexte, renseigné une fois le chargement réussi.</summary>
    public PluginInstance? Plugin { get; set; }

    protected override Assembly? Load(AssemblyName assemblyName)
        => assemblyName.Name is { } name && HostAssemblies.Contains(name)
            ? null
            : _resolver.ResolveAssemblyToPath(assemblyName) is { } path
                ? LoadFromAssemblyPath(path)
                : null;

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        => _resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } path
            ? LoadUnmanagedDllFromPath(path)
            : IntPtr.Zero;
}
