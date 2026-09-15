using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.KnowledgeBase.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Import;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Reconvertit en Markdown les articles de la base de connaissances dont le contenu est resté en
/// HTML.
///
/// La conversion n'a lieu qu'à l'import : le Markdown est écrit une fois pour toutes, donc un
/// article repris avant que la conversion ne fonctionne garde son HTML indéfiniment, quelle que
/// soit la version du code qui l'affiche ensuite. Relancer l'import les corrigerait, mais au prix
/// des retouches faites depuis dans GlpiNg, et à condition que la base GLPI source soit encore
/// joignable — deux conditions qu'on ne peut pas exiger pour réparer des données déjà là.
///
/// Écrite en action automatique plutôt qu'en bouton : l'écran des actions automatiques donne déjà
/// l'exécution manuelle, l'historique et l'activation, et rien de tout cela n'avait à être
/// réinventé.
/// </summary>
public sealed class KnowledgeBaseHtmlReconversionCronTask(
    GlpiNgDbContext db,
    EventLogService eventLog,
    ILogger<KnowledgeBaseHtmlReconversionCronTask> logger) : ICronTask
{
    /// <summary>Auteur porté par la révision et l'entrée d'historique : ce n'est personne.</summary>
    private const string MaintenanceUser = "Maintenance";

    public string Key => "kb_html_reconversion";

    public string Name => "Reconversion HTML de la base de connaissances";

    public string Description =>
        "Reconvertit en Markdown les articles importés dont le contenu est resté en HTML. "
        + "L'état antérieur est archivé en révision avant chaque réécriture, donc restaurable "
        + "depuis la fiche de l'article. Sans effet sur les articles déjà convertis.";

    /// <summary>
    /// Quotidienne. La tâche ne coûte presque rien une fois le rattrapage fait : les pré-filtres
    /// SQL écartent en base tout article ne portant ni chevron ni entité échappée, et il n'en
    /// reste alors plus aucun à lire.
    /// </summary>
    public int DefaultFrequencyMinutes => 1440;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // Deux restrictions, toutes deux là pour ne pas abîmer du contenu sain.
        //
        // SourceGlpiId : seuls les articles repris de GLPI sont concernés. Ceux rédigés dans
        // GlpiNg sont du Markdown par construction — l'éditeur n'en produit pas d'autre — et un
        // article qui documenterait volontairement du HTML n'a pas à être réécrit.
        //
        // Les deux pré-filtres, traduits en LIKE, pour ne pas rapatrier toute la base à chaque
        // passage. Le second n'est pas un raffinement : un contenu resté échappé ne porte aucun
        // chevron, et le seul « < » l'aurait écarté alors qu'il est exactement aussi cassé.
        // La vraie décision se prend ensuite, sur une liste d'éléments HTML nommés.
        List<KnowledgeBaseArticle> candidates = await db.Set<KnowledgeBaseArticle>()
            .Where(article => article.SourceGlpiId != null
                              && (article.Content.Contains("<") || article.Content.Contains("&lt;")))
            .ToListAsync(cancellationToken);

        int converted = 0;
        int skipped = 0;

        foreach (KnowledgeBaseArticle article in candidates)
        {
            if (!GlpiHtmlToMarkdown.ContainsHtml(article.Content))
            {
                continue;
            }

            string markdown = GlpiHtmlToMarkdown.Convert(article.Content);

            // Une conversion qui rend le vide, qui ne change rien, ou qui laisse du HTML derrière
            // elle n'a rien réglé : mieux vaut laisser l'article tel quel et le signaler que le
            // remplacer par pire.
            if (string.IsNullOrWhiteSpace(markdown)
                || markdown == article.Content
                || GlpiHtmlToMarkdown.ContainsHtml(markdown))
            {
                skipped++;
                continue;
            }

            int lastNumber = await db.Set<KnowledgeBaseArticleRevision>()
                .Where(revision => revision.ArticleId == article.Id)
                .MaxAsync(revision => (int?)revision.Number, cancellationToken) ?? 0;

            // L'état antérieur est archivé avant d'être remplacé : c'est ce qui rend l'opération
            // réversible depuis la fiche, et donc acceptable sur des données qu'on n'a pas relues.
            db.Set<KnowledgeBaseArticleRevision>().Add(new KnowledgeBaseArticleRevision
            {
                ArticleId = article.Id,
                Number = lastNumber + 1,
                Subject = article.Subject,
                Content = article.Content,
                EditorName = MaintenanceUser,
                RevisedAt = DateTime.UtcNow,
            });

            db.Set<KnowledgeBaseArticleHistoryEntry>().Add(new KnowledgeBaseArticleHistoryEntry
            {
                ArticleId = article.Id,
                User = MaintenanceUser,
                Field = "Contenu",
                Description = $"Contenu reconverti du HTML vers le Markdown, révision n°{lastNumber + 1} archivée.",
            });

            article.Content = markdown;
            article.UpdatedAt = DateTime.UtcNow;
            article.LastEditorName = MaintenanceUser;

            converted++;
        }

        if (converted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // Compte rendu dans le journal des évènements (/admin/logs), et pas seulement dans les
        // traces : le journal d'exécution des actions automatiques n'enregistre un message qu'en
        // cas d'échec, si bien qu'une tâche qui réussit sans rien convertir y est indiscernable
        // d'une tâche qui a tout converti. Sans ce compte rendu, « la conversion ne fonctionne
        // pas » n'a aucun moyen d'être instruit.
        string summary =
            $"Reconversion HTML : {candidates.Count} article(s) examiné(s), {converted} reconverti(s), "
            + $"{skipped} laissé(s) tel(s) quel(s) faute d'une conversion exploitable.";

        logger.LogInformation("Base de connaissances — {Summary}", summary);

        await eventLog.LogAsync(
            "knowledgebase",
            converted == 0 && candidates.Count > 0 ? EventLogLevel.Warning : EventLogLevel.Info,
            summary,
            cancellationToken: cancellationToken);
    }
}
