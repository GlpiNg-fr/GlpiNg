using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Écrit dans le journal des évènements système consulté sur /admin/logs (voir la doc de
/// <see cref="EventLogEntry"/>).
///
/// Chaque écriture se fait dans son propre contexte court, et non dans celui de la requête ou du
/// circuit courant. C'est délibéré, et c'est une correction : en partageant le contexte appelant,
/// <c>SaveChanges</c> validait aussi tout ce qui y était en attente — donc une ligne de journal
/// échouait dès qu'une écriture sans rapport était en erreur sur le même contexte. Le journal
/// devenait muet exactement quand il servait le plus, et journaliser depuis un bloc <c>catch</c>
/// relançait l'exception d'origine au lieu de la tracer.
///
/// L'effet de bord inverse disparaît aussi : écrire une ligne de journal ne valide plus, au
/// passage, des modifications que l'appelant n'avait pas encore décidé d'enregistrer.
/// </summary>
public class EventLogService(IDbContextFactory<GlpiNgDbContext> dbFactory)
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
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

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
