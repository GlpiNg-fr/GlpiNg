namespace GlpiNg.Modules.Cron.Models;

/// <summary>
/// Historique des exécutions d'une <see cref="Abstractions.Cron.ICronTask"/> (planifiées ou
/// déclenchées manuellement depuis /config/automatic-actions), affiché sur la fiche de l'action.
/// Borné aux dernières entrées par tâche (voir AutomaticActionStore.RecordRunAsync) plutôt que
/// conservé indéfiniment, à la manière de QueuedNotification (voir Queue.razor Take(500)).
/// </summary>
public class AutomaticActionRunLog
{
    public int Id { get; set; }

    public required string TaskKey { get; set; }

    public DateTime RanAt { get; set; } = DateTime.UtcNow;

    public bool Success { get; set; }

    public long DurationMs { get; set; }

    public string? Error { get; set; }
}
