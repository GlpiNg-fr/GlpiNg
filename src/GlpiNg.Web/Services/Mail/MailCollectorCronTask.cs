using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Mail;

/// <summary>
/// Relève les collecteurs de courriels actifs (équivalent de la tâche <c>mailgate</c> de GLPI),
/// réglable depuis /config/automatic-actions comme tout <see cref="ICronTask"/>.
///
/// Un collecteur en échec n'arrête pas les suivants : <c>MailCollectorService</c> inscrit l'erreur
/// sur le collecteur et rend la main.
/// </summary>
public sealed class MailCollectorCronTask(
    GlpiNgDbContext db,
    MailCollectorService collectorService) : ICronTask
{
    public string Key => "mail_collector";

    public string Name => "Collecteurs de courriels";

    public string Description =>
        "Relève les boîtes IMAP déclarées dans « Collecteurs » et ouvre un ticket par message non lu, "
        + "avec son expéditeur en demandeur et ses pièces jointes en documents. "
        + "Un collecteur désactivé n'est pas relevé.";

    /// <summary>
    /// Toutes les dix minutes : un courriel qui ouvre un ticket n'a pas la seconde pour enjeu, et
    /// la tâche ouvre une connexion IMAP par collecteur à chaque passage.
    /// </summary>
    public int DefaultFrequencyMinutes => 10;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        List<int> collectorIds = await db.MailCollectors
            .Where(collector => collector.IsActive)
            .Select(collector => collector.Id)
            .ToListAsync(cancellationToken);

        foreach (int collectorId in collectorIds)
        {
            await collectorService.CollectAsync(collectorId, cancellationToken);
        }
    }
}
