namespace GlpiNg.Modules.Cron.Models;

/// <summary>
/// Ligne unique (Id fixe) stockant l'intervalle d'exécution du service cron, réglable
/// depuis /config → "Configuration générale" → "Système" (voir SystemSection.razor).
/// En base plutôt que dans appsettings.json car c'est la valeur elle-même — pas
/// seulement sa lecture — qui doit changer à chaud : voir CronIntervalState.
/// </summary>
public class CronSettings
{
    public int Id { get; set; }

    public int IntervalMinutes { get; set; } = 60;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
