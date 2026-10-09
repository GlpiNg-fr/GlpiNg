using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Abstractions.Menu;
using GlpiNg.Modules.Abstractions.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GlpiNg.Web.Services.Plugins;

public static class PluginServiceCollectionExtensions
{
    /// <summary>Clé sous laquelle les contributions de l'hôte et des modules sont déplacées.</summary>
    private const string HostKey = "glping:host";

    /// <summary>
    /// Contrats par lesquels un plugin contribue à l'hôte. Les consommateurs les résolvent tous par
    /// <c>IEnumerable&lt;T&gt;</c> (menu, rapports, actions automatiques) ; en ajouter un ici suffit
    /// à ce qu'il accepte les plugins chargés à chaud.
    /// </summary>
    internal static readonly HashSet<Type> Contributions = [typeof(IMenuProvider), typeof(IReportProvider), typeof(ICronTask)];

    /// <summary>
    /// Branche le chargement à chaud des plugins (voir <see cref="PluginRegistry"/>). À appeler
    /// <b>après</b> tous les modules et après <c>AddControllers</c> : les contributions déjà
    /// enregistrées sont déplacées, et l'activateur de contrôleurs de MVC est décoré.
    /// </summary>
    public static IServiceCollection AddPluginHost(this IServiceCollection services)
    {
        services.AddSingleton<PluginRegistry>();
        services.AddScoped<PluginScopes>();
        services.AddScoped<IComponentActivator, PluginComponentActivator>();
#if NET11_0_OR_GREATER
        services.AddSingleton<IComponentPropertyActivator, PluginComponentPropertyActivator>();
#endif

        services.AddSingleton<PluginActionDescriptorChangeProvider>();
        services.AddSingleton<IActionDescriptorChangeProvider>(sp => sp.GetRequiredService<PluginActionDescriptorChangeProvider>());

        ServiceDescriptor controllerActivator = services.LastOrDefault(d => d.ServiceType == typeof(IControllerActivator))
            ?? throw new InvalidOperationException("AddPluginHost doit être appelé après AddControllers.");
        Type innerType = controllerActivator.ImplementationType
            ?? throw new InvalidOperationException("Activateur de contrôleurs MVC inattendu : enregistré sans type d'implémentation.");
        services.Replace(ServiceDescriptor.Describe(
            typeof(IControllerActivator),
            sp => new PluginControllerActivator((IControllerActivator)ActivatorUtilities.CreateInstance(sp, innerType)),
            controllerActivator.Lifetime));

        AddContribution<IMenuProvider>(services);
        AddContribution<IReportProvider>(services);
        AddContribution<ICronTask>(services);

        return services;
    }

    /// <summary>
    /// Fait renvoyer à <c>IEnumerable&lt;T&gt;</c> les <typeparamref name="T"/> de l'hôte <b>et</b> des
    /// plugins actifs, sans toucher aux consommateurs. Le conteneur essaie un enregistrement exact
    /// d'<c>IEnumerable&lt;T&gt;</c> avant de construire lui-même la liste ; les enregistrements
    /// existants sont donc déplacés sous une clé, pour que celui-ci les retrouve sans se rappeler
    /// lui-même. Durées de vie conservées : un singleton de module reste un singleton.
    /// </summary>
    private static void AddContribution<T>(IServiceCollection services) where T : class
    {
        for (int i = 0; i < services.Count; i++)
        {
            ServiceDescriptor descriptor = services[i];
            if (descriptor.ServiceType != typeof(T) || descriptor.IsKeyedService)
            {
                continue;
            }

            services[i] = descriptor.ImplementationInstance is { } instance
                ? new ServiceDescriptor(typeof(T), HostKey, instance)
                : descriptor.ImplementationFactory is { } factory
                    ? new ServiceDescriptor(typeof(T), HostKey, (sp, _) => factory(sp), descriptor.Lifetime)
                    : new ServiceDescriptor(typeof(T), HostKey, descriptor.ImplementationType!, descriptor.Lifetime);
        }

        // Transient : chaque résolution voit les plugins actifs à cet instant. Résolu depuis une
        // portée (requête, circuit, tick cron), jamais depuis la racine : PluginScopes est scoped.
        services.AddTransient<IEnumerable<T>>(sp =>
            [.. sp.GetKeyedServices<T>(HostKey), .. sp.GetRequiredService<PluginScopes>().Contributions<T>()]);
    }
}
