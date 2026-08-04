namespace GlpiNg.Modules.Abstractions.Cron;

/// <summary>
/// Action périodique contribuée par un module et exécutée par le service cron de l'hôte
/// (voir GlpiNg.Modules.Cron.CronBackgroundService) à chaque tick, au même intervalle
/// global pour toutes les tâches — même principe de contribution que <see
/// cref="Menu.IMenuProvider"/> : l'hôte résout <c>IEnumerable&lt;ICronTask&gt;</c> et
/// n'a pas besoin de connaître les modules qui en fournissent.
/// </summary>
public interface ICronTask
{
    /// <summary>Nom affiché dans les journaux d'exécution.</summary>
    string Name { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
