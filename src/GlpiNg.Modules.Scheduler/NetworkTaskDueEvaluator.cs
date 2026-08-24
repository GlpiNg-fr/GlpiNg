using GlpiNg.Modules.Deployment.Models;

namespace GlpiNg.Modules.Scheduler;

/// <summary>
/// Détermine si une <see cref="NetworkTask"/> doit être lancée automatiquement "maintenant" —
/// même logique que <see cref="DeploymentTaskDueEvaluator"/> pour une DeploymentTask, sans les
/// champs préparation/réveil d'agents qui n'ont pas d'équivalent ici (voir la doc de NetworkTask).
/// </summary>
public static class NetworkTaskDueEvaluator
{
    public static bool IsDue(NetworkTask task, DateTime now)
    {
        if (!task.IsActive || task.ScheduledStartTime is not { } start || now < start)
        {
            return false;
        }

        if (task.ScheduledEndTime is { } end && now > end)
        {
            return false;
        }

        if (task.LastLaunchedAt is { } lastLaunched && lastLaunched >= start)
        {
            return false;
        }

        if (task.ExecutionTimeSlot is { Entries.Count: > 0 } slot && !IsWithinTimeSlot(slot, now))
        {
            return false;
        }

        return true;
    }

    private static bool IsWithinTimeSlot(TimeSlot slot, DateTime now)
    {
        TimeOnly time = TimeOnly.FromDateTime(now);
        return slot.Entries.Any(entry => entry.DayOfWeek == now.DayOfWeek
            && time >= entry.StartTime
            && time <= entry.EndTime);
    }
}
