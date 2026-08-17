using GlpiNg.Modules.Deployment.Models;

namespace GlpiNg.Modules.Scheduler;

/// <summary>
/// Détermine si une <see cref="DeploymentTask"/> doit être lancée automatiquement "maintenant" —
/// logique pure, sans accès base, pour rester testable indépendamment de
/// <see cref="DeploymentTaskSchedulerCronTask"/>. Compare en heure serveur locale
/// (<see cref="DateTime.Now"/>), cohérent avec la saisie de
/// <see cref="DeploymentTask.ScheduledStartTime"/>/<see cref="DeploymentTask.ScheduledEndTime"/>
/// via un input HTML <c>datetime-local</c> (pas de fuseau horaire stocké) et avec
/// <see cref="TimeSlotEntry.StartTime"/>/<see cref="TimeSlotEntry.EndTime"/> (<see cref="TimeOnly"/>,
/// sans fuseau non plus).
/// </summary>
public static class DeploymentTaskDueEvaluator
{
    public static bool IsDue(DeploymentTask task, DateTime now)
    {
        if (!task.IsActive || task.ScheduledStartTime is not { } start || now < start)
        {
            return false;
        }

        if (task.ScheduledEndTime is { } end && now > end)
        {
            return false;
        }

        // Déjà déclenchée (automatiquement ici ou manuellement via "Lancer maintenant" — les deux
        // mettent à jour LastLaunchedAt de la même façon, voir DeploymentTaskLaunchService) depuis
        // l'ouverture de cette fenêtre : ne pas relancer à chaque tick tant qu'une nouvelle fenêtre
        // (ScheduledStartTime déplacé dans le futur) n'est pas configurée.
        if (task.LastLaunchedAt is { } lastLaunched && lastLaunched >= start)
        {
            return false;
        }

        // ExecutionTimeSlot sans entrée équivaut à "pas de restriction" plutôt qu'à "jamais" :
        // reprend le comportement le moins surprenant déjà choisi pour DeployComputerGroup
        // dynamique (voir DeployGroupCriteriaEvaluator.Matches), mais dans l'autre sens ici
        // puisqu'un créneau vide n'a pas de sens fonctionnel distinct de "aucun créneau choisi".
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
