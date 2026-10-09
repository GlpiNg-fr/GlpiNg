using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
#if NET11_0_OR_GREATER
using System.Reflection;
#endif
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace GlpiNg.Web.Services.Plugins;

/// <summary>
/// Crée les composants Blazor. Ceux d'un plugin reçoivent par leur constructeur les services du
/// plugin (<see cref="PluginServiceProvider"/>), que le conteneur de l'hôte ne connaît pas ; ceux
/// de l'hôte sont créés comme le ferait l'activateur par défaut du framework, interne et donc
/// reproduit ici. Scoped : le <c>Renderer</c> le résout dans la portée du circuit ou de la requête.
/// </summary>
internal sealed class PluginComponentActivator(IServiceProvider services) : IComponentActivator
{
    private static readonly ConcurrentDictionary<Type, ObjectFactory> HostFactories = new();

    public IComponent CreateInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType)
    {
        if (!typeof(IComponent).IsAssignableFrom(componentType))
        {
            throw new ArgumentException($"The type {componentType.FullName} does not implement {nameof(IComponent)}.", nameof(componentType));
        }

        if (PluginRegistry.OwnerOf(componentType) is { } plugin)
        {
            return (IComponent)plugin.Create(componentType, services.GetRequiredService<PluginScopes>().For(plugin));
        }

        ObjectFactory factory = HostFactories.GetOrAdd(componentType, static type => ActivatorUtilities.CreateFactory(type, Type.EmptyTypes));
        return (IComponent)factory(services, []);
    }
}

#if NET11_0_OR_GREATER
/// <summary>
/// Remplit les propriétés <c>[Inject]</c> (<c>@inject</c>) des composants. .NET 11 ouvre ce point
/// d'extension : un composant de plugin y reçoit aussi les services du plugin, ce qu'avant seul son
/// constructeur pouvait faire. Ceux de l'hôte sont servis comme par l'implémentation par défaut,
/// interne au framework.
/// </summary>
internal sealed class PluginComponentPropertyActivator : IComponentPropertyActivator
{
    private const BindingFlags InjectableProperties =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<Type, Action<IServiceProvider, IComponent>> HostActivators = new();

    public Action<IServiceProvider, IComponent> GetActivator([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType)
    {
        if (PluginRegistry.OwnerOf(componentType) is not { } plugin)
        {
            return HostActivators.GetOrAdd(componentType, CreateActivator);
        }

        // Mis en cache dans le plugin, pas ici : le cache part avec lui au déchargement.
        return plugin.PropertyActivators.GetOrAdd(componentType, type =>
        {
            Action<IServiceProvider, IComponent> inject = CreateActivator(type);
            return (services, component) => inject(services.GetRequiredService<PluginScopes>().For(plugin), component);
        });
    }

    private static Action<IServiceProvider, IComponent> CreateActivator(Type componentType)
    {
        List<(PropertyInfo Property, object? Key)> injectables = [];
        HashSet<string> seen = [];

        for (Type? type = componentType; type is not null && type != typeof(object); type = type.BaseType)
        {
            foreach (PropertyInfo property in type.GetProperties(InjectableProperties))
            {
                if (seen.Add(property.Name) && property.GetCustomAttribute<InjectAttribute>() is { } inject)
                {
                    injectables.Add((property, inject.Key));
                }
            }
        }

        if (injectables.Count == 0)
        {
            return static (_, _) => { };
        }

        return (services, component) =>
        {
            foreach ((PropertyInfo property, object? key) in injectables)
            {
                object? service = key is null
                    ? services.GetService(property.PropertyType)
                    : (services as IKeyedServiceProvider)?.GetKeyedService(property.PropertyType, key);

                property.SetValue(component, service ?? throw new InvalidOperationException(
                    $"Cannot provide a value for property '{property.Name}' on type '{componentType.FullName}'. " +
                    $"There is no registered service of type '{property.PropertyType}'."));
            }
        };
    }
}
#endif

/// <summary>
/// Crée les contrôleurs : ceux d'un plugin avec ses services, ceux de l'hôte par l'activateur que
/// MVC avait enregistré, décoré plutôt que remplacé.
/// </summary>
internal sealed class PluginControllerActivator(IControllerActivator inner) : IControllerActivator
{
    public object Create(ControllerContext context)
    {
        Type type = context.ActionDescriptor.ControllerTypeInfo.AsType();

        return PluginRegistry.OwnerOf(type) is { } plugin
            ? plugin.Create(type, context.HttpContext.RequestServices.GetRequiredService<PluginScopes>().For(plugin))
            : inner.Create(context);
    }

    public void Release(ControllerContext context, object controller)
    {
        if (PluginRegistry.OwnerOf(controller.GetType()) is null)
        {
            inner.Release(context, controller);
        }
        else
        {
            (controller as IDisposable)?.Dispose();
        }
    }

    public ValueTask ReleaseAsync(ControllerContext context, object controller)
    {
        if (PluginRegistry.OwnerOf(controller.GetType()) is null)
        {
            return inner.ReleaseAsync(context, controller);
        }

        if (controller is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        (controller as IDisposable)?.Dispose();
        return default;
    }
}

/// <summary>
/// Signale à MVC que la liste des contrôleurs a changé (plugin chargé ou déchargé) : il reconstruit
/// alors ses actions et ses endpoints, sur-le-champ, pendant <see cref="Signal"/>.
/// </summary>
public sealed class PluginActionDescriptorChangeProvider : IActionDescriptorChangeProvider
{
    private CancellationTokenSource _source = new();

    public IChangeToken GetChangeToken() => new CancellationChangeToken(_source.Token);

    internal void Signal() => Interlocked.Exchange(ref _source, new CancellationTokenSource()).Cancel();
}
