using GlpiNg.Modules.Abstractions.Cron;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GlpiNg.Modules.Cron;

/// <summary>
/// Service principal du module Cron : attend <see cref="CronIntervalState.Interval"/>
/// puis exécute chaque <see cref="ICronTask"/> enregistré par les modules (voir
/// AddCronModule et, côté hôte, les <c>services.AddScoped&lt;ICronTask, ...&gt;()</c>
/// dans Program.cs). Singleton (IHostedService) : une nouvelle portée DI est ouverte à
/// chaque lecture/écriture de <see cref="Models.CronSettings"/> et à chaque tick, pour ne
/// pas garder un DbContext scoped ouvert sur toute la durée de vie du service.
/// </summary>
public sealed class CronBackgroundService(
    IServiceScopeFactory scopeFactory,
    CronIntervalState state,
    ILogger<CronBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshIntervalAsync(stoppingToken);

            TimeSpan interval = state.Interval;
            CancellationToken resetToken = state.ResetToken;
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, resetToken);

            try
            {
                await Task.Delay(interval, linked.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // Intervalle modifié depuis /config (CronIntervalState.UpdateInterval) :
                // on relance l'attente avec la nouvelle valeur sans exécuter les tâches.
                continue;
            }

            await RunTasksAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Relit l'intervalle en base à chaque tick (pas seulement au démarrage) : au premier
    /// démarrage après l'installation, la table CronSettings peut ne pas encore exister
    /// (migrations pas encore appliquées, voir MigrationsGateMiddleware) — on garde alors
    /// l'intervalle courant et on retente au tick suivant, plutôt que de laisser l'exception
    /// remonter et arrêter tout le host (BackgroundServiceExceptionBehavior par défaut =
    /// StopHost). Ça permet aussi de reprendre l'intervalle réel dès que l'admin applique
    /// les migrations depuis /update, sans redémarrer le serveur.
    /// </summary>
    private async Task RefreshIntervalAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            CronSettingsStore store = scope.ServiceProvider.GetRequiredService<CronSettingsStore>();
            state.Initialize(await store.GetIntervalMinutesAsync(stoppingToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cron : lecture de l'intervalle impossible, valeur courante conservée ({Interval})", state.Interval);
        }
    }

    /// <summary>
    /// N'exécute une tâche que si elle est activée et que sa propre fréquence est échue depuis sa
    /// dernière exécution (voir Models.AutomaticActionState) — avant l'ajout de cet état par tâche,
    /// toutes les ICronTask s'exécutaient sans condition à chaque tick.
    /// </summary>
    private async Task RunTasksAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IEnumerable<ICronTask> tasks = scope.ServiceProvider.GetServices<ICronTask>();
        AutomaticActionStore store = scope.ServiceProvider.GetRequiredService<AutomaticActionStore>();
        AutomaticActionRunner runner = scope.ServiceProvider.GetRequiredService<AutomaticActionRunner>();

        foreach (ICronTask task in tasks)
        {
            Models.AutomaticActionState state = await store.EnsureStateAsync(task.Key, task.DefaultFrequencyMinutes, stoppingToken);
            if (!state.IsEnabled)
            {
                continue;
            }

            int frequencyMinutes = state.FrequencyMinutes ?? task.DefaultFrequencyMinutes;
            if (state.LastRunAt is DateTime lastRunAt && DateTime.UtcNow - lastRunAt < TimeSpan.FromMinutes(frequencyMinutes))
            {
                continue;
            }

            logger.LogInformation("Cron : exécution de la tâche {TaskName}", task.Name);
            await runner.RunAsync(task, stoppingToken);
        }
    }
}
