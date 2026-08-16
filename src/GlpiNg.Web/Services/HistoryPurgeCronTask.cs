using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Équivalent réduit de la tâche cron GLPI "purgelogs" : purge les entrées d'historique
/// (Entités/Groupes/Utilisateurs/Profils) plus anciennes que le réglage "Purger toutes
/// les entrées de l'historique" de l'onglet /config → "Purge de l'historique" (voir
/// <see cref="PurgeSettings.RetentionMonths"/>, clé "All").
///
/// PurgeSettings expose un délai par catégorie d'action (ajout, suppression, ...) à la
/// manière de GLPI, mais les entrées d'historique de GlpiNg ne portent pas cette
/// catégorisation (leur champ "Field" est un libellé humain, ex. "Nom", "Commentaires" —
/// voir GlpiEntityHistoryEntry) : seul le réglage global "All" est donc appliqué,
/// uniformément aux quatre tables.
/// </summary>
public sealed class HistoryPurgeCronTask(GlpiNgDbContext db, SettingsCacheService settingsStore) : ICronTask
{
    private const string SectionName = "PurgeSettings";

    public string Name => "Purge de l'historique";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        PurgeSettings settings = await settingsStore.ReadSectionAsync<PurgeSettings>(SectionName);
        int months = settings.RetentionMonths.GetValueOrDefault("All", 0);
        if (months == 0)
        {
            // 0 = conserver indéfiniment (voir PurgeSettings.BuildDefaults).
            return;
        }

        // -1 = supprimer tout de suite : seuil = maintenant, donc tout ce qui existe déjà est purgé.
        DateTime threshold = months < 0 ? DateTime.UtcNow : DateTime.UtcNow.AddMonths(-months);

        await db.EntityHistoryEntries.Where(h => h.OccurredAt < threshold).ExecuteDeleteAsync(cancellationToken);
        await db.GroupHistoryEntries.Where(h => h.OccurredAt < threshold).ExecuteDeleteAsync(cancellationToken);
        await db.UserHistoryEntries.Where(h => h.OccurredAt < threshold).ExecuteDeleteAsync(cancellationToken);
        await db.ProfileHistoryEntries.Where(h => h.OccurredAt < threshold).ExecuteDeleteAsync(cancellationToken);
    }
}
