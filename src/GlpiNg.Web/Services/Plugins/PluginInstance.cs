using System.Collections.Concurrent;
using System.Reflection;
#if NET11_0_OR_GREATER
using Microsoft.AspNetCore.Components;
#endif
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.FileProviders;

namespace GlpiNg.Web.Services.Plugins;

/// <summary>
/// Un plugin chargé : son contexte, son assembly, et les services qu'il a déclarés dans son
/// <c>ConfigureServices</c> — gardés ici plutôt que dans le conteneur de l'hôte, figé au démarrage,
/// pour pouvoir apparaître et disparaître à chaud (voir <see cref="PluginServiceProvider"/>).
/// </summary>
public sealed class PluginInstance : IAsyncDisposable
{
    private readonly Dictionary<Type, ServiceDescriptor[]> _services;
    private readonly ConcurrentDictionary<Type, ObjectFactory> _factories = new();

    internal PluginInstance(
        string name,
        PluginLoadContext context,
        Assembly assembly,
        string folder,
        IReadOnlyList<string> routes,
        IReadOnlyList<ServiceDescriptor> services,
        IReadOnlyList<string> warnings,
        IServiceProvider hostServices)
    {
        Name = name;
        Context = context;
        Assembly = assembly;
        Routes = routes;
        Warnings = warnings;
        Part = new AssemblyPart(assembly);
        _services = services.GroupBy(d => d.ServiceType).ToDictionary(g => g.Key, g => g.ToArray());

        string webRoot = Path.Combine(folder, "wwwroot");
        WebRoot = Directory.Exists(webRoot) ? new PhysicalFileProvider(webRoot) : null;

        // Portée de la vie du plugin : ses singletons y sont mis en cache, et libérés avec lui.
        Root = new PluginServiceProvider(this, hostServices);
    }

    /// <summary>Nom du plugin : celui de son dossier et de son assembly.</summary>
    public string Name { get; }

    public Assembly Assembly { get; }

    /// <summary>Gabarits des pages du plugin, normalisés (voir <c>PluginRegistry.NormalizeRoute</c>).</summary>
    public IReadOnlyList<string> Routes { get; }

    /// <summary>Ce qui a été ignoré au chargement, à montrer à l'administrateur.</summary>
    public IReadOnlyList<string> Warnings { get; }

    internal PluginLoadContext Context { get; }

    /// <summary>Partie MVC : ajoutée au chargement, elle expose les contrôleurs du plugin.</summary>
    internal AssemblyPart Part { get; }

    /// <summary>Dossier <c>wwwroot</c> du plugin, servi sous <c>/_content/{Nom}/</c>.</summary>
    internal IFileProvider? WebRoot { get; }

    internal PluginServiceProvider Root { get; }

#if NET11_0_OR_GREATER
    /// <summary>Injecteurs <c>[Inject]</c> des composants du plugin (voir <c>PluginComponentPropertyActivator</c>).</summary>
    internal ConcurrentDictionary<Type, Action<IServiceProvider, IComponent>> PropertyActivators { get; } = new();
#endif

    /// <summary>Enregistrements du plugin pour un type de service, dans leur ordre de déclaration.</summary>
    internal IReadOnlyList<ServiceDescriptor> Services(Type serviceType)
        => _services.TryGetValue(serviceType, out ServiceDescriptor[]? descriptors) ? descriptors : [];

    /// <summary>
    /// Instancie un composant ou un contrôleur du plugin, constructeur servi par
    /// <paramref name="services"/>. Le cache des fabriques vit ici, pas dans un champ statique :
    /// il part avec le plugin.
    /// </summary>
    internal object Create(Type type, IServiceProvider services)
        => _factories.GetOrAdd(type, static t => ActivatorUtilities.CreateFactory(t, Type.EmptyTypes))(services, []);

    public async ValueTask DisposeAsync()
    {
        await Root.DisposeAsync();
        (WebRoot as IDisposable)?.Dispose();
    }
}
