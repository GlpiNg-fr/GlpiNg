namespace GlpiNg.Modules.Abstractions.Cron;

/// <summary>
/// Action périodique contribuée par un module et exécutée par le service cron de l'hôte
/// (voir GlpiNg.Modules.Cron.CronBackgroundService) — même principe de contribution que
/// <see cref="Menu.IMenuProvider"/> : l'hôte résout <c>IEnumerable&lt;ICronTask&gt;</c> et
/// n'a pas besoin de connaître les modules qui en fournissent.
///
/// Équivalent des "Actions automatiques" de GLPI (glpi_crontasks) : chaque tâche a son propre
/// état persisté (activée/désactivée, fréquence, dernière exécution — voir
/// GlpiNg.Modules.Cron.Models.AutomaticActionState) géré par CronBackgroundService, qui ne
/// vérifie l'échéance de chaque tâche qu'à ses propres ticks (voir CronIntervalState) : la
/// fréquence d'une tâche ne peut donc jamais être plus fine que l'intervalle global du service
/// cron réglé dans /config → "Configuration générale" → "Système".
/// </summary>
public interface ICronTask
{
    /// <summary>
    /// Identifiant stable et invariant (ex. "history_purge"), indépendant du <see cref="Name"/>
    /// affiché : sert de clé à <see cref="Models.AutomaticActionState"/> et ne doit jamais changer
    /// une fois publié, sous peine de faire perdre son état à la tâche (nouvelle ligne recréée
    /// avec les réglages par défaut).
    /// </summary>
    string Key { get; }

    /// <summary>Nom affiché dans le journal d'exécution et la liste des actions automatiques.</summary>
    string Name { get; }

    /// <summary>Description affichée sur la fiche de l'action automatique (/config/automatic-actions).</summary>
    string Description { get; }

    /// <summary>
    /// Fréquence par défaut (en minutes) avant la première configuration explicite par un
    /// administrateur — voir <see cref="Models.AutomaticActionState.FrequencyMinutes"/>.
    /// </summary>
    int DefaultFrequencyMinutes { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
