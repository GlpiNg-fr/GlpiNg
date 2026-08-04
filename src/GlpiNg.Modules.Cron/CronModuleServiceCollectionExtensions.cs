using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GlpiNg.Modules.Cron;

/// <summary>
/// Point d'enregistrement du module Cron dans le conteneur DI de l'hôte, même principe
/// que <c>InventoryModuleServiceCollectionExtensions.AddInventoryModule</c>. Dépend du
/// DbContext de base (via <see cref="CronSettingsStore"/>) donc doit être appelé après
/// que l'hôte a enregistré son DbContext concret — voir Program.cs.
/// </summary>
public static class CronModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCronModule(this IServiceCollection services)
    {
        services.AddSingleton<CronIntervalState>();
        services.AddScoped<CronSettingsStore>();
        services.AddHostedService<CronBackgroundService>();

        return services;
    }
}
