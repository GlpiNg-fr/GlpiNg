using GlpiNg.Web.Data;
using GlpiNg.Web.Models;

namespace GlpiNg.Web.Services;

/// <summary>
/// Écrit dans le journal des évènements système consulté sur /admin/logs (voir la doc de
/// <see cref="EventLogEntry"/>). Scoped comme <c>NotificationDispatchService</c> : injecté
/// directement avec le GlpiNgDbContext de la requête/du circuit Blazor courant plutôt que via
/// une IDbContextFactory.
/// </summary>
public class EventLogService(GlpiNgDbContext db)
{
    public async Task LogAsync(
        string service,
        EventLogLevel level,
        string message,
        string? itemType = null,
        int? itemId = null,
        string? itemLabel = null,
        CancellationToken cancellationToken = default)
    {
        db.EventLogEntries.Add(new EventLogEntry
        {
            Service = service,
            Level = level,
            Message = message,
            ItemType = itemType,
            ItemId = itemId,
            ItemLabel = itemLabel,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
