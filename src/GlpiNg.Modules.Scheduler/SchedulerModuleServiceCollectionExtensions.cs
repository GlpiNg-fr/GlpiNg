using GlpiNg.Modules.Abstractions.Cron;
using Microsoft.Extensions.DependencyInjection;

namespace GlpiNg.Modules.Scheduler;

/// <summary>
/// Point d'enregistrement du module Scheduler dans le conteneur DI de l'hôte, même principe que
/// <c>GlpiNg.Modules.Cron.CronModuleServiceCollectionExtensions.AddCronModule</c>. Contribue deux
/// <see cref="ICronTask"/> (<see cref="DeploymentTaskSchedulerCronTask"/> et
/// <see cref="NetworkTaskSchedulerCronTask"/>) qui s'exécutent au même tick partagé que les autres
/// (agents orphelins, purge d'historique, file de notifications — voir CronBackgroundService)
/// plutôt que d'ouvrir leur propre boucle de fond : ce module se contente de fermer la boucle
/// laissée ouverte par les champs de planification de DeploymentTask/NetworkTask (voir leur doc) en
/// réutilisant l'infrastructure cron déjà en place, comme
/// GlpiNg.Modules.Inventory.AgentCleanupCronTask le fait déjà pour ses propres besoins.
/// </summary>
public static class SchedulerModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulerModule(this IServiceCollection services)
    {
        services.AddScoped<ICronTask, DeploymentTaskSchedulerCronTask>();
        services.AddScoped<ICronTask, NetworkTaskSchedulerCronTask>();
        return services;
    }
}
