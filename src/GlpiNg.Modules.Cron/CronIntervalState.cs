namespace GlpiNg.Modules.Cron;

/// <summary>
/// Détient l'intervalle courant du service cron en mémoire (singleton) et permet de le
/// modifier à chaud : <see cref="UpdateInterval"/> annule le jeton d'attente courant, ce
/// qui réveille immédiatement <c>CronBackgroundService</c> pour qu'il relise le nouvel
/// intervalle au lieu d'attendre la fin du tick en cours — c'est ce qui permet à un
/// changement fait depuis /config de s'appliquer sans redémarrer le serveur.
/// </summary>
public sealed class CronIntervalState
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 1440;
    public const int DefaultMinutes = 60;

    private readonly object _lock = new();
    private TimeSpan _interval = TimeSpan.FromMinutes(DefaultMinutes);
    private CancellationTokenSource _resetSource = new();

    public TimeSpan Interval
    {
        get { lock (_lock) { return _interval; } }
    }

    public CancellationToken ResetToken
    {
        get { lock (_lock) { return _resetSource.Token; } }
    }

    public static int Clamp(int minutes) => Math.Clamp(minutes, MinMinutes, MaxMinutes);

    /// <summary>Fixe l'intervalle initial au démarrage du service, sans réveiller personne.</summary>
    public void Initialize(int minutes)
    {
        lock (_lock)
        {
            _interval = TimeSpan.FromMinutes(Clamp(minutes));
        }
    }

    /// <summary>Change l'intervalle et réveille immédiatement l'attente en cours.</summary>
    public void UpdateInterval(int minutes)
    {
        CancellationTokenSource previous;
        lock (_lock)
        {
            _interval = TimeSpan.FromMinutes(Clamp(minutes));
            previous = _resetSource;
            _resetSource = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }
}
