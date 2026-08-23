using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GlpiNg.Modules.Scheduler;

/// <summary>
/// Tâche cron (voir GlpiNg.Modules.Cron) qui ferme la boucle laissée ouverte par
/// <see cref="DeploymentTask.ScheduledStartTime"/>/<see cref="DeploymentTask.ScheduledEndTime"/>/
/// <see cref="DeploymentTask.ExecutionTimeSlotId"/> (voir leur doc, jusqu'ici purement
/// déclaratifs) : à chaque tick du service cron partagé, recherche les tâches actives dont la
/// fenêtre planifiée est ouverte et pas encore lancée pour cette fenêtre (voir
/// <see cref="DeploymentTaskDueEvaluator"/>), et les lance via
/// <see cref="DeploymentTaskLaunchService"/> — même mécanique que le bouton "Lancer maintenant"
/// de TaskDetail, déclenchée automatiquement plutôt que par un clic.
///
/// Ne gère volontairement pas <see cref="DeploymentTask.PreparationTimeSlotId"/> (pas de
/// pré-téléchargement de paquets côté GlpiNg, les agents tirent le paquet entier au moment du
/// job) ni <see cref="DeploymentTask.AgentWakeUpIntervalMinutes"/>/
/// <see cref="DeploymentTask.AgentWakeUpCount"/> (GlpiNg ne sait pas émettre de paquet
/// Wake-on-LAN) — ces champs restent purement déclaratifs, capturés pour la parité de formulaire
/// avec GLPI-Inventory uniquement.
///
/// Comme <c>GlpiNg.Modules.Inventory.AgentCleanupCronTask</c>, injecte le DbContext de base directement
/// (scoped) plutôt qu'une IDbContextFactory : <c>ICronTask</c> est résolu depuis une portée DI
/// fraîche à chaque tick (voir CronBackgroundService.RunTasksAsync), donc pas de risque de
/// réutiliser un DbContext entre deux exécutions.
/// </summary>
public sealed class DeploymentTaskSchedulerCronTask(
    DbContext db,
    DeploymentTaskLaunchService launchService,
    ILogger<DeploymentTaskSchedulerCronTask> logger) : ICronTask
{
    public string Key => "deployment_scheduler";

    public string Name => "Lancement automatique des tâches de déploiement planifiées";

    public string Description =>
        "Recherche les tâches de déploiement actives dont la fenêtre planifiée est ouverte et pas " +
        "encore lancée pour cette fenêtre, et les lance.";

    public int DefaultFrequencyMinutes => 5;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.Now;

        List<DeploymentTask> candidates = await db.Set<DeploymentTask>()
            .AsNoTracking()
            .Include(t => t.ExecutionTimeSlot)
            .ThenInclude(slot => slot!.Entries)
            .Where(t => t.IsActive && t.ScheduledStartTime != null)
            .ToListAsync(cancellationToken);

        foreach (DeploymentTask task in candidates)
        {
            if (!DeploymentTaskDueEvaluator.IsDue(task, now))
            {
                continue;
            }

            DeploymentTaskLaunchResult result = await launchService.LaunchAsync(task.Id, cancellationToken);
            if (result.Success)
            {
                logger.LogInformation(
                    "Scheduler : tâche « {TaskName} » (#{TaskId}) lancée automatiquement — {Message}",
                    task.Name, task.Id, result.Message);
            }
            else
            {
                logger.LogWarning(
                    "Scheduler : tâche « {TaskName} » (#{TaskId}) due mais non lancée — {Message}",
                    task.Name, task.Id, result.Message);
            }
        }
    }
}
