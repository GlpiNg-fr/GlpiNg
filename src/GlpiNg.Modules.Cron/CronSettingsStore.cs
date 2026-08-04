using GlpiNg.Modules.Cron.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Modules.Cron;

/// <summary>
/// Lit/écrit la ligne unique <see cref="CronSettings"/>. Dépend du <see cref="DbContext"/>
/// de base plutôt que du DbContext concret de l'hôte, comme les autres services de module
/// (voir GlpiMySqlImportService dans GlpiNg.Modules.Inventory).
/// </summary>
public sealed class CronSettingsStore(DbContext db)
{
    public async Task<int> GetIntervalMinutesAsync(CancellationToken cancellationToken = default)
    {
        CronSettings? settings = await db.Set<CronSettings>().FirstOrDefaultAsync(cancellationToken);
        return settings?.IntervalMinutes ?? CronIntervalState.DefaultMinutes;
    }

    public async Task SetIntervalMinutesAsync(int minutes, CancellationToken cancellationToken = default)
    {
        int clamped = CronIntervalState.Clamp(minutes);
        CronSettings? settings = await db.Set<CronSettings>().FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            db.Set<CronSettings>().Add(new CronSettings { IntervalMinutes = clamped });
        }
        else
        {
            settings.IntervalMinutes = clamped;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
