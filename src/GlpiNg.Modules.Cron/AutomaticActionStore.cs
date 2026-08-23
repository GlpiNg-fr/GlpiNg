using GlpiNg.Modules.Cron.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Modules.Cron;

/// <summary>
/// Lit/écrit l'état par tâche (<see cref="AutomaticActionState"/>) et son historique d'exécution
/// (<see cref="AutomaticActionRunLog"/>). Dépend du <see cref="DbContext"/> de base plutôt que du
/// DbContext concret de l'hôte, même principe que <see cref="CronSettingsStore"/>.
/// </summary>
public sealed class AutomaticActionStore(DbContext db)
{
    /// <summary>Nombre d'entrées d'historique conservées par tâche.</summary>
    private const int MaxRunLogEntriesPerTask = 50;

    public Task<List<AutomaticActionState>> GetAllStatesAsync(CancellationToken cancellationToken = default) =>
        db.Set<AutomaticActionState>().OrderBy(s => s.TaskKey).ToListAsync(cancellationToken);

    /// <summary>Récupère l'état d'une tâche, en créant sa ligne (avec la fréquence par défaut fournie) si absente.</summary>
    public async Task<AutomaticActionState> EnsureStateAsync(string taskKey, int defaultFrequencyMinutes, CancellationToken cancellationToken = default)
    {
        AutomaticActionState? state = await db.Set<AutomaticActionState>()
            .FirstOrDefaultAsync(s => s.TaskKey == taskKey, cancellationToken);

        if (state is not null)
        {
            return state;
        }

        state = new AutomaticActionState { TaskKey = taskKey, FrequencyMinutes = defaultFrequencyMinutes };
        db.Set<AutomaticActionState>().Add(state);
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }

    public async Task SetEnabledAsync(string taskKey, bool enabled, CancellationToken cancellationToken = default)
    {
        AutomaticActionState? state = await db.Set<AutomaticActionState>()
            .FirstOrDefaultAsync(s => s.TaskKey == taskKey, cancellationToken);

        if (state is null)
        {
            return;
        }

        state.IsEnabled = enabled;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetFrequencyAsync(string taskKey, int frequencyMinutes, CancellationToken cancellationToken = default)
    {
        AutomaticActionState? state = await db.Set<AutomaticActionState>()
            .FirstOrDefaultAsync(s => s.TaskKey == taskKey, cancellationToken);

        if (state is null)
        {
            return;
        }

        state.FrequencyMinutes = CronIntervalState.Clamp(frequencyMinutes);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Met à jour l'état "dernière exécution" et ajoute une ligne d'historique, en purgeant les
    /// entrées au-delà de <see cref="MaxRunLogEntriesPerTask"/> — appelé par
    /// <see cref="AutomaticActionRunner"/>, que l'exécution soit planifiée ou manuelle.
    /// </summary>
    public async Task RecordRunAsync(string taskKey, bool success, long durationMs, string? error, CancellationToken cancellationToken = default)
    {
        AutomaticActionState? state = await db.Set<AutomaticActionState>()
            .FirstOrDefaultAsync(s => s.TaskKey == taskKey, cancellationToken);

        if (state is not null)
        {
            state.LastRunAt = DateTime.UtcNow;
            state.LastRunSuccess = success;
            state.LastRunDurationMs = durationMs;
            state.LastRunError = error;
        }

        db.Set<AutomaticActionRunLog>().Add(new AutomaticActionRunLog
        {
            TaskKey = taskKey,
            Success = success,
            DurationMs = durationMs,
            Error = error,
        });

        await db.SaveChangesAsync(cancellationToken);

        List<AutomaticActionRunLog> overflow = await db.Set<AutomaticActionRunLog>()
            .Where(l => l.TaskKey == taskKey)
            .OrderByDescending(l => l.RanAt)
            .Skip(MaxRunLogEntriesPerTask)
            .ToListAsync(cancellationToken);

        if (overflow.Count > 0)
        {
            db.Set<AutomaticActionRunLog>().RemoveRange(overflow);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<List<AutomaticActionRunLog>> GetRunLogAsync(string taskKey, int take = 50, CancellationToken cancellationToken = default) =>
        db.Set<AutomaticActionRunLog>()
            .Where(l => l.TaskKey == taskKey)
            .OrderByDescending(l => l.RanAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}
