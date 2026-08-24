using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GlpiNg.Modules.Scheduler;

/// <summary>
/// Tâche cron équivalente à <see cref="DeploymentTaskSchedulerCronTask"/> pour les
/// <see cref="NetworkTask"/> (Découverte réseau / Inventaire réseau SNMP) : recherche les tâches
/// réseau actives dont la fenêtre planifiée est ouverte et pas encore lancée pour cette fenêtre
/// (voir <see cref="NetworkTaskDueEvaluator"/>), et les lance via
/// <see cref="NetworkTaskLaunchService"/>.
/// </summary>
public sealed class NetworkTaskSchedulerCronTask(
    DbContext db,
    NetworkTaskLaunchService launchService,
    ILogger<NetworkTaskSchedulerCronTask> logger) : ICronTask
{
    public string Key => "network_task_scheduler";

    public string Name => "Lancement automatique des tâches réseau planifiées";

    public string Description =>
        "Recherche les tâches réseau (découverte/inventaire SNMP) actives dont la fenêtre planifiée " +
        "est ouverte et pas encore lancée pour cette fenêtre, et les lance.";

    public int DefaultFrequencyMinutes => 5;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.Now;

        List<NetworkTask> candidates = await db.Set<NetworkTask>()
            .AsNoTracking()
            .Include(t => t.ExecutionTimeSlot)
            .ThenInclude(slot => slot!.Entries)
            .Where(t => t.IsActive && t.ScheduledStartTime != null)
            .ToListAsync(cancellationToken);

        foreach (NetworkTask task in candidates)
        {
            if (!NetworkTaskDueEvaluator.IsDue(task, now))
            {
                continue;
            }

            NetworkTaskLaunchResult result = await launchService.LaunchAsync(task.Id, cancellationToken);
            if (result.Success)
            {
                logger.LogInformation(
                    "Scheduler : tâche réseau « {TaskName} » (#{TaskId}) lancée automatiquement — {Message}",
                    task.Name, task.Id, result.Message);
            }
            else
            {
                logger.LogWarning(
                    "Scheduler : tâche réseau « {TaskName} » (#{TaskId}) due mais non lancée — {Message}",
                    task.Name, task.Id, result.Message);
            }
        }
    }
}
