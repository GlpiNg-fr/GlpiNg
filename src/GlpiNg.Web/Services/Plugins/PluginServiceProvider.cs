namespace GlpiNg.Web.Services.Plugins;

/// <summary>
/// Services vus par un plugin, pour une portée de l'hôte (requête, circuit Blazor, tick cron) :
/// d'abord ceux que le plugin a déclarés, sinon ceux de l'hôte.
///
/// Le conteneur de l'hôte est figé une fois construit ; celui-ci est le seul endroit où les
/// services d'un plugin chargé à chaud existent. Durées de vie : un singleton vit dans la portée
/// racine du plugin (<see cref="PluginInstance.Root"/>, libérée au déchargement), un service scoped
/// dans cette portée-ci, un transient est recréé à chaque demande. Ce qui est créé ici et jetable
/// est libéré avec la portée, comme le ferait le conteneur de l'hôte.
/// </summary>
// ponytail: pas de détection des dépendances circulaires (dépassement de pile), ni de génériques
// ouverts ni de services keyed côté plugin (signalés au chargement) — à ajouter si un plugin en a besoin.
internal sealed class PluginServiceProvider(PluginInstance plugin, IServiceProvider host)
    : IServiceProvider, IKeyedServiceProvider, IServiceProviderIsService, IDisposable, IAsyncDisposable
{
    private readonly Dictionary<ServiceDescriptor, object> _cached = [];
    private readonly List<object> _disposables = [];

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider)
            || serviceType == typeof(IKeyedServiceProvider)
            || serviceType == typeof(IServiceProviderIsService))
        {
            return this;
        }

        IReadOnlyList<ServiceDescriptor> own = plugin.Services(serviceType);
        if (own.Count > 0)
        {
            return Resolve(own[^1]);
        }

        // IEnumerable<T> : ceux de l'hôte, puis ceux du plugin. Sauf pour les contributions, que
        // l'hôte renvoie déjà avec celles de tous les plugins (voir PluginServiceCollectionExtensions).
        if (serviceType.IsConstructedGenericType
            && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            && serviceType.GenericTypeArguments[0] is var item
            && !PluginServiceCollectionExtensions.Contributions.Contains(item)
            && plugin.Services(item).Count > 0)
        {
            object[] all = [.. (IEnumerable<object>?)host.GetService(serviceType) ?? [], .. ResolveOwn(item)];
            Array typed = Array.CreateInstance(item, all.Length);
            Array.Copy(all, typed, all.Length);
            return typed;
        }

        return host.GetService(serviceType);
    }

    /// <summary>Seulement les services que le plugin a lui-même déclarés pour ce type.</summary>
    internal IEnumerable<object> ResolveOwn(Type serviceType) => plugin.Services(serviceType).Select(Resolve);

    public object? GetKeyedService(Type serviceType, object? serviceKey)
        => (host as IKeyedServiceProvider)?.GetKeyedService(serviceType, serviceKey);

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
        => GetKeyedService(serviceType, serviceKey)
            ?? throw new InvalidOperationException($"Aucun service keyed {serviceType} pour la clé « {serviceKey} ».");

    public bool IsService(Type serviceType)
        => plugin.Services(serviceType).Count > 0
            || serviceType == typeof(IServiceProvider)
            || host.GetService<IServiceProviderIsService>()?.IsService(serviceType) == true;

    private object Resolve(ServiceDescriptor descriptor) => descriptor.Lifetime switch
    {
        ServiceLifetime.Singleton when this != plugin.Root => plugin.Root.Resolve(descriptor),
        ServiceLifetime.Transient => Create(descriptor),
        _ => Cached(descriptor),
    };

    private object Cached(ServiceDescriptor descriptor)
    {
        // Verrou réentrant : créer un service peut en résoudre un autre de la même portée.
        lock (_cached)
        {
            if (!_cached.TryGetValue(descriptor, out object? service))
            {
                service = Create(descriptor);
                _cached[descriptor] = service;
            }

            return service;
        }
    }

    private object Create(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is { } instance)
        {
            // Fourni tout fait : il appartient au plugin, pas à la portée.
            return instance;
        }

        object service = descriptor.ImplementationFactory is { } factory
            ? factory(this)
            : ActivatorUtilities.CreateInstance(this, descriptor.ImplementationType!);

        if (service is IDisposable or IAsyncDisposable)
        {
            lock (_disposables)
            {
                _disposables.Add(service);
            }
        }

        return service;
    }

    private object[] TakeDisposables()
    {
        lock (_disposables)
        {
            object[] taken = [.. _disposables];
            _disposables.Clear();
            Array.Reverse(taken);
            return taken;
        }
    }

    public void Dispose()
    {
        foreach (object service in TakeDisposables())
        {
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else
            {
                ((IAsyncDisposable)service).DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (object service in TakeDisposables())
        {
            if (service is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                ((IDisposable)service).Dispose();
            }
        }
    }
}

/// <summary>
/// Pont entre une portée de l'hôte et les plugins : un <see cref="PluginServiceProvider"/> par plugin,
/// créé à la première demande et libéré avec la portée de l'hôte (service scoped).
///
/// Un plugin déchargé pendant que la portée vit encore — un circuit Blazor ouvert sur sa page — garde
/// ses services jusqu'à la fin de cette portée : déchargement différé plutôt que libération sous
/// les pieds d'un utilisateur.
/// </summary>
public sealed class PluginScopes(PluginRegistry registry, IServiceProvider services) : IDisposable, IAsyncDisposable
{
    private readonly Dictionary<PluginInstance, PluginServiceProvider> _providers = [];

    internal PluginServiceProvider For(PluginInstance plugin)
    {
        lock (_providers)
        {
            if (!_providers.TryGetValue(plugin, out PluginServiceProvider? provider))
            {
                provider = new PluginServiceProvider(plugin, services);
                _providers[plugin] = provider;
            }

            return provider;
        }
    }

    /// <summary>Contributions <typeparamref name="T"/> des plugins actifs, dans l'ordre de chargement.</summary>
    internal IEnumerable<T> Contributions<T>()
        => registry.Active.SelectMany(plugin => For(plugin).ResolveOwn(typeof(T))).Cast<T>();

    private PluginServiceProvider[] TakeProviders()
    {
        lock (_providers)
        {
            PluginServiceProvider[] taken = [.. _providers.Values];
            _providers.Clear();
            return taken;
        }
    }

    public void Dispose()
    {
        foreach (PluginServiceProvider provider in TakeProviders())
        {
            provider.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (PluginServiceProvider provider in TakeProviders())
        {
            await provider.DisposeAsync();
        }
    }
}
