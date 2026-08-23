namespace GlpiNg.Modules.Cron.Models;

/// <summary>
/// État persisté d'une <see cref="Abstractions.Cron.ICronTask"/>, une ligne par tâche, clé par
/// <see cref="Abstractions.Cron.ICronTask.Key"/> — équivalent réduit de glpi_crontasks : permet
/// de désactiver une action ou de régler sa propre fréquence indépendamment des autres, alors
/// qu'avant l'ajout de cette table toutes les tâches s'exécutaient sans condition à chaque tick
/// du service cron partagé (voir CronBackgroundService.RunTasksAsync). Créée à la demande (voir
/// AutomaticActionStore.EnsureStateAsync) plutôt que pré-remplie par migration, pour rester en
/// phase même si des tâches sont ajoutées/retirées au fil des versions.
/// </summary>
public class AutomaticActionState
{
    public int Id { get; set; }

    public required string TaskKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Fréquence effective en minutes ; null = utilise ICronTask.DefaultFrequencyMinutes.</summary>
    public int? FrequencyMinutes { get; set; }

    public DateTime? LastRunAt { get; set; }

    public bool? LastRunSuccess { get; set; }

    public long? LastRunDurationMs { get; set; }

    public string? LastRunError { get; set; }
}
