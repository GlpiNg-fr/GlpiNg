using System.Diagnostics;
using GlpiNg.Modules.Abstractions.Cron;
using Microsoft.Extensions.Logging;

namespace GlpiNg.Modules.Cron;

/// <summary>
/// Exécute une <see cref="ICronTask"/> et enregistre le résultat via <see cref="AutomaticActionStore"/>
/// — chemin commun au tick planifié de <see cref="CronBackgroundService"/> et au bouton
/// "Lancer maintenant" de /config/automatic-actions, pour ne pas dupliquer la mesure de durée et la
/// gestion des exceptions entre les deux appelants.
/// </summary>
public sealed class AutomaticActionRunner(
    IEnumerable<ICronTask> tasks,
    AutomaticActionStore store,
    ILogger<AutomaticActionRunner> logger)
{
    /// <summary>Exécute la tâche identifiée par <paramref name="taskKey"/>. Retourne false si la clé est inconnue ou si l'exécution a levé une exception.</summary>
    public async Task<bool> RunAsync(string taskKey, CancellationToken cancellationToken)
    {
        ICronTask? task = tasks.FirstOrDefault(t => t.Key == taskKey);
        return task is not null && await RunAsync(task, cancellationToken);
    }

    public async Task<bool> RunAsync(ICronTask task, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            await task.RunAsync(cancellationToken);
            await store.RecordRunAsync(task.Key, success: true, stopwatch.ElapsedMilliseconds, error: null, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Cron : échec de la tâche {TaskName}", task.Name);
            await store.RecordRunAsync(task.Key, success: false, stopwatch.ElapsedMilliseconds, ex.Message, cancellationToken);
            return false;
        }
    }
}
