using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Passe en erreur les <see cref="DeploymentJob"/> restés « en cours » sans que l'agent ne les
/// termine. getJobs marque le job en cours dès qu'il le sert (voir
/// AgentController.HandleGetJobsCoreAsync), mais rien ne garantit que l'agent le traite : s'il
/// rejette le JSON, il ne le dit qu'en debug dans son propre journal (« bad JSON: ... ») puis
/// « No Deploy job found », et le serveur n'en reçoit jamais rien — le job restait alors en cours
/// indéfiniment, constaté en conditions réelles sur un paquet avec fichier.
///
/// Deux délais, parce que les deux silences ne veulent pas dire la même chose :
///  - aucun setStatus du tout (journal vide) : l'agent envoie « starting » dans la seconde qui suit
///    getJobs, dans la même exécution de la tâche (GLPI::Agent::Task::Deploy::processRemote). Son
///    absence après <see cref="NeverAcknowledgedDelay"/> signifie qu'il n'a pas accepté le job ;
///  - un job commencé mais jamais terminé (agent coupé, poste éteint en pleine installation) : un
///    téléchargement ou une installation peut légitimement durer, d'où le délai large de
///    <see cref="UnfinishedDelay"/>.
///
/// Placée dans l'hôte plutôt que dans le module Déploiement pour publier la même notification
/// « job en erreur » qu'un échec rapporté par l'agent.
/// </summary>
public sealed class DeploymentJobTimeoutCronTask(
    GlpiNgDbContext db,
    NotificationDispatchService notificationDispatch,
    ILogger<DeploymentJobTimeoutCronTask> logger) : ICronTask
{
    private static readonly TimeSpan NeverAcknowledgedDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UnfinishedDelay = TimeSpan.FromHours(24);

    public string Key => "deployment_job_timeout";

    public string Name => "Expiration des jobs de déploiement";

    public string Description =>
        "Passe en erreur les jobs de déploiement restés en cours : sans aucun retour de l'agent " +
        $"après {NeverAcknowledgedDelay.TotalMinutes:0} minutes (job refusé par l'agent), ou non " +
        $"terminés après {UnfinishedDelay.TotalHours:0} heures.";

    public int DefaultFrequencyMinutes => 5;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        DateTime neverAcknowledgedThreshold = now - NeverAcknowledgedDelay;
        DateTime unfinishedThreshold = now - UnfinishedDelay;

        List<DeploymentJob> staleJobs = await db.DeploymentJobs
            .Include(j => j.Package)
            .Include(j => j.Agent)
            .ThenInclude(a => a!.Computer)
            .Where(j => j.Status == DeploymentStatus.Running
                && j.StartedAt != null
                && ((string.IsNullOrEmpty(j.Log) && j.StartedAt < neverAcknowledgedThreshold)
                    || j.StartedAt < unfinishedThreshold))
            .ToListAsync(cancellationToken);

        if (staleJobs.Count == 0)
        {
            return;
        }

        foreach (DeploymentJob job in staleJobs)
        {
            string reason = string.IsNullOrEmpty(job.Log)
                ? $"aucun retour de l'agent {NeverAcknowledgedDelay.TotalMinutes:0} minutes après l'envoi du job : "
                    + "il l'a probablement refusé (voir « bad JSON » dans son journal en debug)"
                : $"job non terminé {UnfinishedDelay.TotalHours:0} heures après son démarrage";

            string logLine = $"[{DateTime.Now:HH:mm:ss}] expiré : {reason} (ko)";
            job.Log = string.IsNullOrEmpty(job.Log) ? logLine : job.Log + Environment.NewLine + logLine;
            job.Status = DeploymentStatus.Error;
            job.CompletedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning("deploy: {Count} job(s) en cours expiré(s) et passé(s) en erreur ({Ids}).",
            staleJobs.Count, string.Join(", ", staleJobs.Select(j => j.Id)));

        foreach (DeploymentJob job in staleJobs)
        {
            Dictionary<string, string?> variables = new()
            {
                ["job.package"] = job.Package?.Name,
                ["job.agent"] = job.Agent?.AgentName ?? job.Agent?.DeviceId,
                ["job.computer"] = job.Agent?.Computer?.Name,
                ["job.status"] = "En erreur",
                ["job.log"] = job.Log,
                ["job.url"] = "/tools/deployments/supervision",
            };

            await notificationDispatch.PublishAsync(
                NotificationEventCatalog.DeploymentJob, NotificationEventCatalog.EventError, job.Id, variables, cancellationToken);
        }
    }
}
