using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GlpiNg.Modules.Scheduler;

/// <summary>
/// Tâche cron équivalente à <see cref="NetworkTaskSchedulerCronTask"/> pour les
/// <see cref="WakeOnLanTask"/> : recherche les tâches de réveil réseau actives dont la fenêtre
/// planifiée est ouverte et pas encore lancée pour cette fenêtre (voir
/// <see cref="WakeOnLanTaskDueEvaluator"/>), et les lance via <see cref="WakeOnLanTaskLaunchService"/>.
/// </summary>
public sealed class WakeOnLanTaskSchedulerCronTask(
    DbContext db,
    WakeOnLanTaskLaunchService launchService,
    ILogger<WakeOnLanTaskSchedulerCronTask> logger) : ICronTask
{
    public string Key => "wakeonlan_task_scheduler";

    public string Name => "Lancement automatique des tâches de réveil réseau planifiées";

    public string Description =>
        "Recherche les tâches de réveil réseau (WakeOnLan) actives dont la fenêtre planifiée " +
        "est ouverte et pas encore lancée pour cette fenêtre, et les lance.";

    public int DefaultFrequencyMinutes => 5;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.Now;

        List<WakeOnLanTask> candidates = await db.Set<WakeOnLanTask>()
            .AsNoTracking()
            .Include(t => t.ExecutionTimeSlot)
            .ThenInclude(slot => slot!.Entries)
            .Where(t => t.IsActive && t.ScheduledStartTime != null)
            .ToListAsync(cancellationToken);

        foreach (WakeOnLanTask task in candidates)
        {
            if (!WakeOnLanTaskDueEvaluator.IsDue(task, now))
            {
                continue;
            }

            WakeOnLanTaskLaunchResult result = await launchService.LaunchAsync(task.Id, cancellationToken);
            if (result.Success)
            {
                logger.LogInformation(
                    "Scheduler : tâche de réveil réseau « {TaskName} » (#{TaskId}) lancée automatiquement — {Message}",
                    task.Name, task.Id, result.Message);
            }
            else
            {
                logger.LogWarning(
                    "Scheduler : tâche de réveil réseau « {TaskName} » (#{TaskId}) due mais non lancée — {Message}",
                    task.Name, task.Id, result.Message);
            }
        }
    }
}
