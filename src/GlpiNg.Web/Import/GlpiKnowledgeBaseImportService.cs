using GlpiNg.Modules.Abstractions.Directory;
using GlpiNg.Modules.Abstractions.Documents;
using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Abstractions.Import;
using GlpiNg.Modules.Abstractions.Notes;
using GlpiNg.Modules.Abstractions.Storage;
using GlpiNg.Modules.KnowledgeBase.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Documents;
using GlpiNg.Web.Models.Notes;
using GlpiNg.Web.Services;
using GlpiNg.Web.Services.Documents;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GlpiNg.Web.Import;

/// <summary>
/// Implémentation de <see cref="IGlpiKnowledgeBaseImportService"/> : reprend, en lecture seule et
/// de façon idempotente, la base de connaissances de la base GLPI source. Hébergée ici pour la
/// même raison que <see cref="GlpiInventoryPluginImportService"/> : seul l'hôte voit à la fois les
/// modèles du module Base de connaissances et les entités, groupes, profils et comptes auxquels
/// les cibles de visibilité renvoient.
///
/// Deux conversions méritent d'être connues avant de lire le code.
///
/// 1. <b>Le contenu change de format.</b> GLPI stocke la réponse d'un article en HTML ; GlpiNg
///    stocke du Markdown et n'interprète jamais de HTML (voir <c>MarkdownRenderer</c>). La
///    conversion est donc faite ici, à l'import, une fois pour toutes — l'alternative aurait été
///    de garder le HTML en base et de l'assainir à chaque affichage, ce que le module refuse par
///    construction. Une mise en forme exotique peut s'y perdre : c'est le prix, et il est annoncé.
/// 2. <b>Les identifiants changent de monde.</b> Tout ce qui est repris est corrélé par
///    <c>SourceGlpiId</c> (catégories, articles) ou par les <c>SourceGlpiId</c> déjà posés par
///    l'import Administration (entités, groupes, profils, comptes). Une cible dont l'objet n'a pas
///    été importé est comptée comme ignorée plutôt que silencieusement oubliée : un article qui
///    perd une restriction devient plus visible qu'il ne l'était.
///
/// Les noms de tables et de colonnes varient selon les versions de GLPI : chaque lecture passe par
/// les colonnes réellement présentes (information_schema), même principe que
/// <c>GlpiMySqlImportService</c>.
/// </summary>
public sealed class GlpiKnowledgeBaseImportService(
    GlpiNgDbContext db,
    EntityTreeCache entityTree,
    DocumentStorageService documentStorage)
    : IGlpiKnowledgeBaseImportService
{
    private const string CategoriesTable = "glpi_knowbaseitemcategories";
    private const string ArticlesTable = "glpi_knowbaseitems";
    private const string RevisionsTable = "glpi_knowbaseitems_revisions";
    private const string DocumentsTable = "glpi_documents";
    private const string DocumentItemsTable = "glpi_documents_items";
    private const string DocumentCategoriesTable = "glpi_documentcategories";
    private const string NotesTable = "glpi_notepads";

    /// <summary>Nom que GLPI donne aux articles dans ses références polymorphes.</summary>
    private const string GlpiArticleItemType = ItemTypes.KnowledgeBaseArticle;

    /// <summary>Tables de visibilité de GLPI et type d'acteur correspondant côté GlpiNg.</summary>
    private static readonly (string Table, string ForeignKey, PrincipalKind Kind)[] TargetTables =
    [
        ("glpi_knowbaseitems_users", "users_id", PrincipalKind.User),
        ("glpi_knowbaseitems_groups", "groups_id", PrincipalKind.Group),
        ("glpi_knowbaseitems_profiles", "profiles_id", PrincipalKind.Profile),
        ("glpi_knowbaseitems_entities", "entities_id", PrincipalKind.Entity),
    ];

    public async Task<GlpiKnowledgeBaseImportAnalysis> AnalyzeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        GlpiKnowledgeBaseImportAnalysis analysis = new();

        await using MySqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        analysis.IsPresent = await TableExistsAsync(connection, ArticlesTable, cancellationToken);

        if (!analysis.IsPresent)
        {
            return analysis;
        }

        analysis.ArticlesCount = await CountAsync(connection, ArticlesTable, cancellationToken);
        analysis.CategoriesCount = await CountAsync(connection, CategoriesTable, cancellationToken);
        analysis.RevisionsCount = await CountAsync(connection, RevisionsTable, cancellationToken);

        foreach ((string table, _, _) in TargetTables)
        {
            analysis.TargetsCount += await CountAsync(connection, table, cancellationToken);
        }

        analysis.DocumentsCount = await CountDocumentLinksAsync(connection, cancellationToken);
        analysis.NotesCount = await CountNotesAsync(connection, cancellationToken);

        return analysis;
    }

    public async Task<GlpiKnowledgeBaseImportResult> RunAsync(
        string connectionString,
        GlpiKnowledgeBaseImportSelection selection,
        IProgress<GlpiImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        GlpiKnowledgeBaseImportResult result = new();

        if (!selection.AnySelected)
        {
            return result;
        }

        await using MySqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Entité de repli : l'import tourne hors session applicative (scope créé par
        // GlpiImportStateService), il n'y a donc pas d'entité active à reprendre — même choix que
        // l'import du plugin d'inventaire.
        int? rootEntityId = entityTree.GetRootEntityId();
        Dictionary<int, int> entityMap = await LoadSourceMapAsync(
            db.Entities.Where(e => e.SourceGlpiId != null).Select(e => new SourceRef(e.SourceGlpiId, e.Id)), cancellationToken);

        if (selection.ImportCategories)
        {
            await ImportCategoriesAsync(connection, entityMap, rootEntityId, result, progress, cancellationToken);
        }

        // Les articles ont besoin des catégories : leur correspondance est relue en base plutôt que
        // conservée de l'étape précédente, pour que « Articles » seul fonctionne aussi sur un
        // import déjà passé.
        Dictionary<int, int> categoryMap = await LoadSourceMapAsync(
            db.Set<KnowledgeBaseCategory>().Where(c => c.SourceGlpiId != null).Select(c => new SourceRef(c.SourceGlpiId, c.Id)), cancellationToken);

        if (selection.ImportArticles)
        {
            await ImportArticlesAsync(connection, categoryMap, entityMap, rootEntityId, result, progress, cancellationToken);
        }

        Dictionary<int, int> articleMap = await LoadSourceMapAsync(
            db.Set<KnowledgeBaseArticle>().Where(a => a.SourceGlpiId != null).Select(a => new SourceRef(a.SourceGlpiId, a.Id)), cancellationToken);

        if (selection.ImportTargets)
        {
            await ImportTargetsAsync(connection, articleMap, entityMap, result, progress, cancellationToken);
        }

        if (selection.ImportRevisions)
        {
            await ImportRevisionsAsync(connection, articleMap, result, progress, cancellationToken);
        }

        if (selection.ImportDocuments)
        {
            await ImportDocumentsAsync(connection, articleMap, entityMap, rootEntityId, selection,
                result, progress, cancellationToken);
        }

        if (selection.ImportNotes)
        {
            await ImportNotesAsync(connection, articleMap, result, progress, cancellationToken);
        }

        return result;
    }

    // ---- Catégories ---------------------------------------------------------------------------

    private async Task ImportCategoriesAsync(
        MySqlConnection connection, Dictionary<int, int> entityMap, int? rootEntityId,
        GlpiKnowledgeBaseImportResult result, IProgress<GlpiImportProgress>? progress, CancellationToken ct)
    {
        HashSet<string> columns = await GetColumnsAsync(connection, CategoriesTable, ct);

        if (columns.Count == 0)
        {
            result.Warnings.Add($"Table « {CategoriesTable} » absente : aucune catégorie reprise.");
            return;
        }

        string sql = $"""
            SELECT id,
                   {ColumnOrNull(columns, "name")},
                   {ColumnOrNull(columns, "comment")},
                   {ColumnOrNull(columns, "knowbaseitemcategories_id")},
                   {ColumnOrNull(columns, "entities_id")},
                   {ColumnOrNull(columns, "is_recursive")}
            FROM `{CategoriesTable}`
            """;

        Dictionary<int, KnowledgeBaseCategory> existing = await db.Set<KnowledgeBaseCategory>()
            .Where(category => category.SourceGlpiId != null)
            .ToDictionaryAsync(category => category.SourceGlpiId!.Value, ct);

        // Parent de chaque catégorie, en identifiants GLPI : il ne peut être traduit qu'une fois
        // toutes les catégories créées, une fille pouvant précéder sa mère dans la table.
        Dictionary<int, int> parentBySourceId = [];
        int seen = 0;

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            int sourceId = reader.GetInt32("id");
            string name = GetNullableString(reader, "name") ?? $"Catégorie {sourceId}";

            if (!existing.TryGetValue(sourceId, out KnowledgeBaseCategory? category))
            {
                category = new KnowledgeBaseCategory { Name = name, SourceGlpiId = sourceId };
                db.Set<KnowledgeBaseCategory>().Add(category);
                existing[sourceId] = category;
                result.CategoriesCreated++;
            }
            else
            {
                category.Name = name;
                result.CategoriesUpdated++;
            }

            category.Comment = GetNullableString(reader, "comment");
            category.EntityId = ResolveEntity(reader, entityMap, rootEntityId);
            category.IsRecursive = GetNullableInt(reader, "is_recursive") == 1;

            if (GetNullableInt(reader, "knowbaseitemcategories_id") is int parentSourceId && parentSourceId > 0)
            {
                parentBySourceId[sourceId] = parentSourceId;
            }

            progress?.Report(new GlpiImportProgress(GlpiImportPhases.KnowledgeBaseCategories, ++seen));
        }

        await db.SaveChangesAsync(ct);

        // Second passage : les identifiants locaux existent enfin, l'arbre peut être recomposé.
        foreach ((int sourceId, int parentSourceId) in parentBySourceId)
        {
            if (existing.TryGetValue(sourceId, out KnowledgeBaseCategory? category)
                && existing.TryGetValue(parentSourceId, out KnowledgeBaseCategory? parent))
            {
                category.ParentId = parent.Id;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ---- Articles -----------------------------------------------------------------------------

    private async Task ImportArticlesAsync(
        MySqlConnection connection, Dictionary<int, int> categoryMap, Dictionary<int, int> entityMap, int? rootEntityId,
        GlpiKnowledgeBaseImportResult result, IProgress<GlpiImportProgress>? progress, CancellationToken ct)
    {
        HashSet<string> columns = await GetColumnsAsync(connection, ArticlesTable, ct);

        if (columns.Count == 0)
        {
            result.Warnings.Add($"Table « {ArticlesTable} » absente : aucun article repris.");
            return;
        }

        // La date de création a changé de nom entre les versions ; « view » est le compteur de
        // consultations.
        string? createdColumn = FirstPresent(columns, "date_creation", "date");

        string sql = $"""
            SELECT id,
                   {ColumnOrNull(columns, "name")},
                   {ColumnOrNull(columns, "answer")},
                   {ColumnOrNull(columns, "is_faq")},
                   {ColumnOrNull(columns, "view")},
                   {ColumnOrNull(columns, "users_id")},
                   {ColumnOrNull(columns, "knowbaseitemcategories_id")},
                   {ColumnOrNull(columns, "entities_id")},
                   {ColumnOrNull(columns, "is_recursive")},
                   {ColumnOrNull(columns, "date_mod")},
                   {ColumnOrNull(columns, "begin_date")},
                   {ColumnOrNull(columns, "end_date")},
                   {(createdColumn is null ? "NULL AS `date_creation`" : $"`{createdColumn}` AS `date_creation`")}
            FROM `{ArticlesTable}`
            """;

        // GLPI 10 a sorti le rattachement de catégorie dans une table de liaison ; les versions
        // antérieures le portaient en colonne. On lit celle qui existe.
        Dictionary<int, int> categoryByArticle = columns.Contains("knowbaseitemcategories_id")
            ? []
            : await LoadArticleCategoryLinksAsync(connection, ct);

        Dictionary<int, (int Id, string Name)> users = await db.Users
            .Where(user => user.SourceGlpiId != null)
            .Select(user => new { Source = user.SourceGlpiId!.Value, user.Id, Name = user.DisplayName ?? user.UserName })
            .ToDictionaryAsync(user => user.Source, user => (user.Id, user.Name), ct);

        Dictionary<int, KnowledgeBaseArticle> existing = await db.Set<KnowledgeBaseArticle>()
            .Where(article => article.SourceGlpiId != null)
            .ToDictionaryAsync(article => article.SourceGlpiId!.Value, ct);

        int seen = 0;

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            int sourceId = reader.GetInt32("id");
            string subject = GetNullableString(reader, "name") ?? $"Article {sourceId}";
            string content = GlpiHtmlToMarkdown.Convert(GetNullableString(reader, "answer"));

            if (!existing.TryGetValue(sourceId, out KnowledgeBaseArticle? article))
            {
                article = new KnowledgeBaseArticle
                {
                    Subject = subject,
                    Content = content,
                    SourceGlpiId = sourceId,
                };

                db.Set<KnowledgeBaseArticle>().Add(article);
                existing[sourceId] = article;
                result.ArticlesCreated++;
            }
            else
            {
                article.Subject = subject;
                article.Content = content;
                result.ArticlesUpdated++;
            }

            article.IsFaq = GetNullableInt(reader, "is_faq") == 1;
            article.ViewCount = GetNullableInt(reader, "view") ?? article.ViewCount;
            article.EntityId = ResolveEntity(reader, entityMap, rootEntityId);
            article.IsRecursive = GetNullableInt(reader, "is_recursive") == 1;
            article.CreatedAt = GetNullableDate(reader, "date_creation") ?? article.CreatedAt;
            article.UpdatedAt = GetNullableDate(reader, "date_mod");

            // Période de visibilité (GLPI 9.2+) : absente des bases plus anciennes, où
            // ColumnOrNull rend NULL — l'article est alors simplement sans borne.
            article.VisibleFrom = GetNullableDate(reader, "begin_date");
            article.VisibleUntil = GetNullableDate(reader, "end_date");

            int? sourceCategoryId = GetNullableInt(reader, "knowbaseitemcategories_id")
                ?? (categoryByArticle.TryGetValue(sourceId, out int linked) ? linked : null);

            article.CategoryId = sourceCategoryId is int categorySourceId && categoryMap.TryGetValue(categorySourceId, out int localCategoryId)
                ? localCategoryId
                : null;

            if (GetNullableInt(reader, "users_id") is int authorSourceId && users.TryGetValue(authorSourceId, out (int Id, string Name) author))
            {
                article.AuthorUserId = author.Id;
                article.AuthorName = author.Name;
            }
            else
            {
                // Auteur inconnu côté GlpiNg (compte non importé, ou supprimé de GLPI) : on le
                // nomme plutôt que de laisser vide, pour que la fiche dise d'où vient l'article.
                article.AuthorName ??= "Import GLPI";
            }

            article.LastEditorName ??= article.AuthorName;

            progress?.Report(new GlpiImportProgress(GlpiImportPhases.KnowledgeBaseArticles, ++seen));
        }

        await db.SaveChangesAsync(ct);

        if (result.ArticlesCreated + result.ArticlesUpdated > 0)
        {
            result.Warnings.Add(
                "Le contenu des articles a été converti du HTML de GLPI vers le Markdown de GlpiNg : "
                + "une mise en forme complexe (HTML brut, styles en ligne, éléments imbriqués) peut avoir été simplifiée.");
        }
    }

    /// <summary>
    /// Rattachement article → catégorie par la table de liaison de GLPI 10. Un article pouvant y
    /// figurer plusieurs fois, seule la première catégorie est retenue : le modèle GlpiNg n'en
    /// porte qu'une, et en choisir une vaut mieux que n'en garder aucune.
    /// </summary>
    private static async Task<Dictionary<int, int>> LoadArticleCategoryLinksAsync(MySqlConnection connection, CancellationToken ct)
    {
        const string table = "glpi_knowbaseitems_knowbaseitemcategories";
        Dictionary<int, int> map = [];

        if (!await TableExistsAsync(connection, table, ct))
        {
            return map;
        }

        string sql = $"SELECT knowbaseitems_id, knowbaseitemcategories_id FROM `{table}`";

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            int articleId = reader.GetInt32("knowbaseitems_id");
            int categoryId = reader.GetInt32("knowbaseitemcategories_id");
            map.TryAdd(articleId, categoryId);
        }

        return map;
    }

    // ---- Cibles de visibilité -----------------------------------------------------------------

    private async Task ImportTargetsAsync(
        MySqlConnection connection, Dictionary<int, int> articleMap, Dictionary<int, int> entityMap,
        GlpiKnowledgeBaseImportResult result, IProgress<GlpiImportProgress>? progress, CancellationToken ct)
    {
        Dictionary<int, int> groupMap = await LoadSourceMapAsync(
            db.Groups.Where(g => g.SourceGlpiId != null).Select(g => new SourceRef(g.SourceGlpiId, g.Id)), ct);
        Dictionary<int, int> profileMap = await LoadSourceMapAsync(
            db.Profiles.Where(p => p.SourceGlpiId != null).Select(p => new SourceRef(p.SourceGlpiId, p.Id)), ct);
        Dictionary<int, int> userMap = await LoadSourceMapAsync(
            db.Users.Where(u => u.SourceGlpiId != null).Select(u => new SourceRef(u.SourceGlpiId, u.Id)), ct);

        // Les cibles déjà en base sont relues pour ne pas les recréer : l'index d'unicité
        // (article, type, cible, portée) ferait échouer tout l'enregistrement au second import.
        var existingTargets = await db.Set<KnowledgeBaseArticleTarget>()
            .Select(target => new { target.ArticleId, target.Type, target.ItemId, target.ScopeEntityId })
            .ToListAsync(ct);

        HashSet<(int ArticleId, PrincipalKind Kind, int ItemId, int? ScopeEntityId)> known =
            [.. existingTargets.Select(target => (target.ArticleId, target.Type, target.ItemId, target.ScopeEntityId))];

        int seen = 0;

        foreach ((string table, string foreignKey, PrincipalKind kind) in TargetTables)
        {
            if (!await TableExistsAsync(connection, table, ct))
            {
                continue;
            }

            Dictionary<int, int> map = kind switch
            {
                PrincipalKind.User => userMap,
                PrincipalKind.Group => groupMap,
                PrincipalKind.Profile => profileMap,
                _ => entityMap,
            };

            // GLPI porte la portée par entité sur les tables groupe et profil (entities_id +
            // is_recursive) et la seule récursivité sur la table entité. Les colonnes sont lues
            // quand elles existent : une base antérieure à leur apparition rend NULL, ce qui vaut
            // « pas de portée » — soit exactement le comportement d'avant.
            HashSet<string> targetColumns = await GetColumnsAsync(connection, table, ct);

            string sql = $"""
                SELECT knowbaseitems_id,
                       `{foreignKey}` AS target_id,
                       {ColumnOrNull(targetColumns, "entities_id")},
                       {ColumnOrNull(targetColumns, "is_recursive")}
                FROM `{table}`
                """;

            await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
            {
                progress?.Report(new GlpiImportProgress(GlpiImportPhases.KnowledgeBaseTargets, ++seen));

                int sourceArticleId = reader.GetInt32("knowbaseitems_id");

                if (GetNullableInt(reader, "target_id") is not int sourceTargetId
                    || !articleMap.TryGetValue(sourceArticleId, out int articleId))
                {
                    result.TargetsSkipped++;
                    continue;
                }

                if (!map.TryGetValue(sourceTargetId, out int itemId))
                {
                    // L'objet visé n'a pas été importé : la cible est perdue, et l'article est donc
                    // plus ouvert côté GlpiNg qu'il ne l'était côté GLPI.
                    result.TargetsSkipped++;
                    continue;
                }

                bool recursive = GetNullableInt(reader, "is_recursive") == 1;

                // Pour une cible entité, entities_id *est* la cible (déjà lue en target_id) : la
                // reprendre comme portée ferait doublon. Pour un utilisateur, la portée n'a pas
                // de sens — désigner quelqu'un nommément ne se restreint pas davantage.
                int? scopeEntityId = kind is PrincipalKind.Group or PrincipalKind.Profile
                    && GetNullableInt(reader, "entities_id") is int sourceScopeId
                    && entityMap.TryGetValue(sourceScopeId, out int mappedScopeId)
                        ? mappedScopeId
                        : null;

                if (!known.Add((articleId, kind, itemId, scopeEntityId)))
                {
                    continue;
                }

                db.Set<KnowledgeBaseArticleTarget>().Add(new KnowledgeBaseArticleTarget
                {
                    ArticleId = articleId,
                    Type = kind,
                    ItemId = itemId,
                    ScopeEntityId = scopeEntityId,
                    IsRecursive = recursive && kind is not PrincipalKind.User,
                });

                result.TargetsImported++;
            }
        }

        await db.SaveChangesAsync(ct);

        if (result.TargetsSkipped > 0)
        {
            result.Warnings.Add(
                $"{result.TargetsSkipped} cible(s) de visibilité ignorée(s) : l'entité, le groupe, le profil ou le compte visé "
                + "n'existe pas dans GlpiNg (importez l'onglet « Administration » d'abord). Les articles concernés sont plus "
                + "largement visibles qu'ils ne l'étaient dans GLPI.");
        }
    }

    // ---- Documents ------------------------------------------------------------------------------

    /// <summary>
    /// Nombre de documents rattachés à un article dans la base source. Compté sur la table de
    /// liaison et non sur <c>glpi_documents</c> : l'import ne reprend que ce qui sert à la base de
    /// connaissances, pas le fonds documentaire entier d'une installation GLPI.
    /// </summary>
    private static async Task<int> CountDocumentLinksAsync(MySqlConnection connection, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, DocumentItemsTable, ct))
        {
            return 0;
        }

        await using MySqlCommand command = new(
            $"SELECT COUNT(*) FROM `{DocumentItemsTable}` WHERE itemtype = @itemtype", connection);
        command.Parameters.AddWithValue("@itemtype", GlpiArticleItemType);

        object? value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    /// <summary>
    /// Reprend les documents rattachés aux articles, leurs catégories, et — si le dossier
    /// <c>files/</c> de GLPI est joignable — le contenu des fichiers.
    ///
    /// GLPI ne stocke pas les fichiers en base : <c>glpi_documents.filepath</c> ne donne qu'un
    /// chemin relatif à ce dossier. Sans lui, l'import crée donc des fiches sans contenu plutôt
    /// que de renoncer — perdre le rattachement ferait disparaître l'information « cet article
    /// s'accompagne de ce mode d'emploi », qui vaut mieux que rien. Elles sont comptées à part et
    /// réparables : téléverser le fichier depuis la fiche de l'article retombe sur la même
    /// empreinte et complète la fiche existante (voir DocumentService.UploadAsync).
    /// </summary>
    private async Task ImportDocumentsAsync(
        MySqlConnection connection, Dictionary<int, int> articleMap, Dictionary<int, int> entityMap,
        int? rootEntityId, GlpiKnowledgeBaseImportSelection selection, GlpiKnowledgeBaseImportResult result,
        IProgress<GlpiImportProgress>? progress, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, DocumentsTable, ct) ||
            !await TableExistsAsync(connection, DocumentItemsTable, ct))
        {
            result.Warnings.Add($"Table « {DocumentsTable} » ou « {DocumentItemsTable} » absente : aucun document repris.");
            return;
        }

        string? glpiFilesPath = selection.GlpiFilesPath;

        // La session vers le partage est ouverte avant tout test d'existence : sur un chemin UNC
        // que le compte du service ne voit pas, Directory.Exists répond « non » aussi bien pour un
        // dossier absent que pour un accès refusé, et le message d'erreur désignerait la mauvaise
        // cause. Sans identifiants, ou sur un chemin local, Connect ne fait rien.
        NetworkShareConnection? share = NetworkShareConnection.None;

        if (!string.IsNullOrWhiteSpace(glpiFilesPath))
        {
            share = NetworkShareConnection.Connect(
                glpiFilesPath, selection.GlpiFilesUserName, selection.GlpiFilesPassword, out string? shareFailure);

            if (share is null)
            {
                result.Warnings.Add(
                    $"Connexion au partage « {glpiFilesPath} » impossible ({shareFailure}) : les documents "
                    + "sont repris sans leur fichier.");
            }
        }

        using (share)
        {
            await ImportDocumentsCoreAsync(
                connection, articleMap, entityMap, rootEntityId, glpiFilesPath, share is not null,
                result, progress, ct);
        }
    }

    /// <summary>
    /// Le gros de l'import des documents, une fois la session vers le partage ouverte (ou établie
    /// inutile). Séparé pour que la session se referme par <c>using</c> autour d'un seul appel
    /// plutôt que de courir jusqu'au bout d'une méthode de deux cents lignes.
    /// </summary>
    private async Task ImportDocumentsCoreAsync(
        MySqlConnection connection, Dictionary<int, int> articleMap, Dictionary<int, int> entityMap,
        int? rootEntityId, string? glpiFilesPath, bool shareOpened, GlpiKnowledgeBaseImportResult result,
        IProgress<GlpiImportProgress>? progress, CancellationToken ct)
    {

        bool hasFiles = shareOpened
            && !string.IsNullOrWhiteSpace(glpiFilesPath)
            && Directory.Exists(glpiFilesPath);

        if (shareOpened && !string.IsNullOrWhiteSpace(glpiFilesPath) && !hasFiles)
        {
            result.Warnings.Add(
                $"Dossier « {glpiFilesPath} » introuvable : les documents sont repris sans leur fichier. "
                + "Vérifiez le chemin (il doit désigner le dossier files/ de GLPI) et les droits de lecture du service.");
        }

        Dictionary<int, int> documentCategoryMap =
            await ImportDocumentCategoriesAsync(connection, entityMap, rootEntityId, ct);

        // Seuls les documents réellement rattachés à un article importé sont repris.
        Dictionary<int, List<int>> articlesByDocument = await LoadDocumentLinksAsync(connection, articleMap, ct);

        if (articlesByDocument.Count == 0)
        {
            // Sortir en silence laissait croire l'import réussi alors qu'il n'avait rien fait, et
            // ne disait pas laquelle des deux causes s'appliquait.
            int totalLinks = await CountDocumentLinksAsync(connection, ct);

            result.Warnings.Add(totalLinks == 0
                ? $"Aucun document rattaché à un article dans la base source : « {DocumentItemsTable} » "
                  + $"ne contient aucune ligne d'itemtype « {GlpiArticleItemType} »."
                : $"{totalLinks} rattachement(s) document/article trouvé(s) dans la base source, mais aucun "
                  + "article correspondant n'a été importé. Cochez « Articles » et relancez : un "
                  + "rattachement ne peut se poser que sur un article déjà repris.");

            return;
        }

        HashSet<string> columns = await GetColumnsAsync(connection, DocumentsTable, ct);

        string sql = $"""
            SELECT id,
                   {ColumnOrNull(columns, "name")},
                   {ColumnOrNull(columns, "filename")},
                   {ColumnOrNull(columns, "filepath")},
                   {ColumnOrNull(columns, "mime")},
                   {ColumnOrNull(columns, "sha1sum")},
                   {ColumnOrNull(columns, "link")},
                   {ColumnOrNull(columns, "comment")},
                   {ColumnOrNull(columns, "documentcategories_id")},
                   {ColumnOrNull(columns, "entities_id")},
                   {ColumnOrNull(columns, "is_recursive")}
            FROM `{DocumentsTable}`
            WHERE id IN ({string.Join(",", articlesByDocument.Keys)})
            """;

        Dictionary<int, Document> existing = await db.Documents
            .Where(document => document.SourceGlpiId != null)
            .ToDictionaryAsync(document => document.SourceGlpiId!.Value, ct);

        // Les rattachements déjà en base, pour ne pas retomber sur l'index d'unicité au second import.
        List<DocumentItem> existingLinks = await db.DocumentItems
            .Where(link => link.ItemType == ItemTypes.KnowledgeBaseArticle)
            .ToListAsync(ct);

        HashSet<(int DocumentId, int ArticleId)> knownLinks =
            [.. existingLinks.Select(link => (link.DocumentId, link.ItemId))];

        int seen = 0;

        // Chemin GLPI d'origine par document, retenu le temps de la lecture : la copie du fichier
        // est différée après la boucle, pour ne pas lire le disque tant que le lecteur MySQL tient
        // la connexion ouverte.
        List<(Document Document, string? SourceFilePath, List<int> ArticleIds)> pending = [];

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.KnowledgeBaseDocuments, ++seen));

            int sourceId = reader.GetInt32("id");
            string fileName = GetNullableString(reader, "filename") ?? $"document-{sourceId}";

            if (!existing.TryGetValue(sourceId, out Document? document))
            {
                document = new Document
                {
                    Name = GetNullableString(reader, "name") ?? fileName,
                    FileName = fileName,
                    // Renseignés plus bas, une fois le fichier lu — ou marqués absents.
                    Sha256 = string.Empty,
                    StoragePath = string.Empty,
                    SourceGlpiId = sourceId,
                    CreatedAt = DateTime.UtcNow,
                };

                db.Documents.Add(document);
                existing[sourceId] = document;
            }
            else
            {
                document.Name = GetNullableString(reader, "name") ?? document.Name;
                document.FileName = fileName;
            }

            document.MimeType = GetNullableString(reader, "mime");
            document.Comment = GetNullableString(reader, "comment");
            document.Link = GetNullableString(reader, "link");
            document.SourceSha1 = GetNullableString(reader, "sha1sum");
            document.EntityId = ResolveEntity(reader, entityMap, rootEntityId);
            document.IsRecursive = GetNullableInt(reader, "is_recursive") == 1;

            if (GetNullableInt(reader, "documentcategories_id") is int sourceCategoryId
                && documentCategoryMap.TryGetValue(sourceCategoryId, out int categoryId))
            {
                document.CategoryId = categoryId;
            }

            pending.Add((document, GetNullableString(reader, "filepath"), articlesByDocument[sourceId]));
        }

        foreach ((Document document, string? sourceFilePath, List<int> articleIds) in pending)
        {
            await CopyDocumentContentAsync(document, sourceFilePath, hasFiles ? glpiFilesPath : null, result, ct);

            foreach (int articleId in articleIds)
            {
                if (document.Id != 0 && knownLinks.Contains((document.Id, articleId)))
                {
                    continue;
                }

                // Posé par la navigation : l'identifiant du document n'est pas encore connu pour
                // une fiche créée à l'instant, et EF s'en charge à l'enregistrement.
                document.Items.Add(new DocumentItem
                {
                    ItemType = ItemTypes.KnowledgeBaseArticle,
                    ItemId = articleId,
                });

                result.DocumentLinksImported++;
            }

            result.DocumentsImported++;
        }

        await db.SaveChangesAsync(ct);

        if (result.DocumentsWithoutContent > 0)
        {
            result.Warnings.Add(
                $"{result.DocumentsWithoutContent} document(s) repris sans leur fichier : "
                + (hasFiles
                    ? "le fichier n'a pas été trouvé dans le dossier files/ indiqué."
                    : "le dossier files/ de GLPI n'a pas été renseigné.")
                + " Les fiches sont créées et rattachées, mais pas téléchargeables ; téléverser le fichier "
                + "depuis l'article complète la fiche existante.");
        }
    }

    /// <summary>
    /// Copie le fichier depuis le dossier <c>files/</c> de GLPI vers la racine de stockage, et
    /// renseigne empreinte, taille et chemin. Marque la fiche « sans contenu » si le fichier n'est
    /// pas lisible, plutôt que de faire échouer l'import entier pour un document manquant.
    /// </summary>
    private async Task CopyDocumentContentAsync(
        Document document, string? sourceFilePath, string? glpiFilesPath,
        GlpiKnowledgeBaseImportResult result, CancellationToken ct)
    {
        // Déjà repris avec son contenu lors d'un passage précédent : ne pas relire le disque.
        if (document.Id != 0 && !document.IsContentMissing && document.Sha256.Length > 0)
        {
            return;
        }

        if (glpiFilesPath is null || string.IsNullOrWhiteSpace(sourceFilePath))
        {
            MarkContentMissing(document, result);
            return;
        }

        // Le chemin vient de la base : il est confiné sous le dossier indiqué, sans quoi un
        // « ../../ » stocké côté GLPI ferait lire n'importe quel fichier du serveur.
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(glpiFilesPath));
        string fullPath = Path.GetFullPath(Path.Combine(root, sourceFilePath.Replace('\\', '/')));

        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || !File.Exists(fullPath))
        {
            MarkContentMissing(document, result);
            return;
        }

        try
        {
            await using FileStream source = File.OpenRead(fullPath);
            (string sha256, long size, string storagePath) = await documentStorage.SaveAsync(source, ct);

            document.Sha256 = sha256;
            document.SizeBytes = size;
            document.StoragePath = storagePath;
            document.IsContentMissing = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MarkContentMissing(document, result);
        }
    }

    /// <summary>
    /// Fiche sans fichier. Une empreinte reste obligatoire (colonne requise, et l'unicité du
    /// contenu s'appuie dessus) : elle est dérivée de l'identifiant GLPI, avec un préfixe littéral
    /// qui la rend impossible à confondre avec un vrai SHA-256 et donc à dédupliquer par erreur.
    /// </summary>
    private static void MarkContentMissing(Document document, GlpiKnowledgeBaseImportResult result)
    {
        if (document.IsContentMissing && document.Sha256.Length > 0)
        {
            return;
        }

        document.Sha256 = $"missing:glping-{document.SourceGlpiId}";
        document.StoragePath = string.Empty;
        document.SizeBytes = 0;
        document.IsContentMissing = true;

        result.DocumentsWithoutContent++;
    }

    /// <summary>Rattachements document → articles importés, en identifiants GLPI côté document.</summary>
    private static async Task<Dictionary<int, List<int>>> LoadDocumentLinksAsync(
        MySqlConnection connection, Dictionary<int, int> articleMap, CancellationToken ct)
    {
        Dictionary<int, List<int>> byDocument = [];

        await using MySqlCommand command = new(
            $"SELECT documents_id, items_id FROM `{DocumentItemsTable}` WHERE itemtype = @itemtype", connection);
        command.Parameters.AddWithValue("@itemtype", GlpiArticleItemType);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            int sourceDocumentId = reader.GetInt32("documents_id");
            int sourceArticleId = reader.GetInt32("items_id");

            // Article non importé (case « Articles » décochée, ou article absent) : le
            // rattachement n'a personne à qui s'accrocher.
            if (!articleMap.TryGetValue(sourceArticleId, out int articleId))
            {
                continue;
            }

            if (!byDocument.TryGetValue(sourceDocumentId, out List<int>? articles))
            {
                byDocument[sourceDocumentId] = articles = [];
            }

            if (!articles.Contains(articleId))
            {
                articles.Add(articleId);
            }
        }

        return byDocument;
    }

    /// <summary>
    /// Catégories de documents (<c>glpi_documentcategories</c>). Arborescentes comme celles de la
    /// base de connaissances, et reconstruites de la même façon : à plat d'abord, parents ensuite,
    /// une fille pouvant précéder sa mère dans la table.
    /// </summary>
    private async Task<Dictionary<int, int>> ImportDocumentCategoriesAsync(
        MySqlConnection connection, Dictionary<int, int> entityMap, int? rootEntityId, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, DocumentCategoriesTable, ct))
        {
            return [];
        }

        HashSet<string> columns = await GetColumnsAsync(connection, DocumentCategoriesTable, ct);

        string sql = $"""
            SELECT id,
                   {ColumnOrNull(columns, "name")},
                   {ColumnOrNull(columns, "comment")},
                   {ColumnOrNull(columns, "documentcategories_id")},
                   {ColumnOrNull(columns, "entities_id")},
                   {ColumnOrNull(columns, "is_recursive")}
            FROM `{DocumentCategoriesTable}`
            """;

        Dictionary<int, DocumentCategory> existing = await db.DocumentCategories
            .Where(category => category.SourceGlpiId != null)
            .ToDictionaryAsync(category => category.SourceGlpiId!.Value, ct);

        Dictionary<int, int> parentBySourceId = [];

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            int sourceId = reader.GetInt32("id");
            string name = GetNullableString(reader, "name") ?? $"Catégorie {sourceId}";

            if (!existing.TryGetValue(sourceId, out DocumentCategory? category))
            {
                category = new DocumentCategory { Name = name, SourceGlpiId = sourceId };
                db.DocumentCategories.Add(category);
                existing[sourceId] = category;
            }
            else
            {
                category.Name = name;
            }

            category.Comment = GetNullableString(reader, "comment");
            category.EntityId = ResolveEntity(reader, entityMap, rootEntityId);
            category.IsRecursive = GetNullableInt(reader, "is_recursive") == 1;

            if (GetNullableInt(reader, "documentcategories_id") is int parentSourceId && parentSourceId > 0)
            {
                parentBySourceId[sourceId] = parentSourceId;
            }
        }

        await db.SaveChangesAsync(ct);

        foreach ((int sourceId, int parentSourceId) in parentBySourceId)
        {
            if (existing.TryGetValue(sourceId, out DocumentCategory? category)
                && existing.TryGetValue(parentSourceId, out DocumentCategory? parent)
                && category.Id != parent.Id)
            {
                category.ParentId = parent.Id;
            }
        }

        await db.SaveChangesAsync(ct);

        return existing.ToDictionary(entry => entry.Key, entry => entry.Value.Id);
    }

    // ---- Révisions ----------------------------------------------------------------------------

    private async Task ImportRevisionsAsync(
        MySqlConnection connection, Dictionary<int, int> articleMap,
        GlpiKnowledgeBaseImportResult result, IProgress<GlpiImportProgress>? progress, CancellationToken ct)
    {
        HashSet<string> columns = await GetColumnsAsync(connection, RevisionsTable, ct);

        if (columns.Count == 0)
        {
            result.Warnings.Add($"Table « {RevisionsTable} » absente (GLPI antérieur à 9.2) : aucune révision reprise.");
            return;
        }

        string sql = $"""
            SELECT knowbaseitems_id,
                   {ColumnOrNull(columns, "revision")},
                   {ColumnOrNull(columns, "name")},
                   {ColumnOrNull(columns, "answer")},
                   {ColumnOrNull(columns, "users_id")},
                   {ColumnOrNull(columns, "date")}
            FROM `{RevisionsTable}`
            """;

        Dictionary<int, string> userNames = await db.Users
            .Where(user => user.SourceGlpiId != null)
            .Select(user => new { Source = user.SourceGlpiId!.Value, Name = user.DisplayName ?? user.UserName })
            .ToDictionaryAsync(user => user.Source, user => user.Name, ct);

        // Numéros déjà présents par article : une révision réimportée, ou une révision GlpiNg
        // portant le même numéro, ferait échouer l'index d'unicité (article, numéro).
        var existingRevisions = await db.Set<KnowledgeBaseArticleRevision>()
            .Select(revision => new { revision.ArticleId, revision.Number })
            .ToListAsync(ct);

        HashSet<(int ArticleId, int Number)> known =
            [.. existingRevisions.Select(revision => (revision.ArticleId, revision.Number))];

        int seen = 0;

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.KnowledgeBaseRevisions, ++seen));

            int sourceArticleId = reader.GetInt32("knowbaseitems_id");

            if (!articleMap.TryGetValue(sourceArticleId, out int articleId))
            {
                continue;
            }

            int number = GetNullableInt(reader, "revision") ?? 0;

            if (!known.Add((articleId, number)))
            {
                continue;
            }

            db.Set<KnowledgeBaseArticleRevision>().Add(new KnowledgeBaseArticleRevision
            {
                ArticleId = articleId,
                Number = number,
                Subject = GetNullableString(reader, "name") ?? string.Empty,
                Content = GlpiHtmlToMarkdown.Convert(GetNullableString(reader, "answer")),
                EditorName = GetNullableInt(reader, "users_id") is int editorId && userNames.TryGetValue(editorId, out string? name)
                    ? name
                    : "Import GLPI",
                RevisedAt = GetNullableDate(reader, "date") ?? DateTime.UtcNow,
            });

            result.RevisionsImported++;
        }

        await db.SaveChangesAsync(ct);
    }

    // ---- Notes ----------------------------------------------------------------------------------

    /// <summary>Notes portées par un article dans la base source (<c>glpi_notepads</c> en itemtype KnowbaseItem).</summary>
    private static async Task<int> CountNotesAsync(MySqlConnection connection, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, NotesTable, ct))
        {
            return 0;
        }

        await using MySqlCommand command = new(
            $"SELECT COUNT(*) FROM `{NotesTable}` WHERE itemtype = @itemtype", connection);
        command.Parameters.AddWithValue("@itemtype", GlpiArticleItemType);

        object? value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    /// <summary>
    /// Reprend les notes libres rattachées aux articles — l'onglet « Notes » que l'hôte rend aux
    /// modules via <see cref="IItemNotes"/>. Corrélées par <see cref="Notepad.SourceGlpiId"/>,
    /// comme le reste de l'import : relancer met à jour une note déjà reprise (contenu modifié
    /// côté GLPI depuis) plutôt que d'en recréer une.
    /// </summary>
    private async Task ImportNotesAsync(
        MySqlConnection connection, Dictionary<int, int> articleMap,
        GlpiKnowledgeBaseImportResult result, IProgress<GlpiImportProgress>? progress, CancellationToken ct)
    {
        HashSet<string> columns = await GetColumnsAsync(connection, NotesTable, ct);

        if (columns.Count == 0)
        {
            result.Warnings.Add($"Table « {NotesTable} » absente : aucune note reprise.");
            return;
        }

        // Même changement de nom que sur les articles (voir ImportArticlesAsync).
        string? createdColumn = FirstPresent(columns, "date_creation", "date");

        string sql = $"""
            SELECT id,
                   items_id,
                   {ColumnOrNull(columns, "content")},
                   {ColumnOrNull(columns, "users_id")},
                   {ColumnOrNull(columns, "users_id_lastupdater")},
                   {ColumnOrNull(columns, "date_mod")},
                   {(createdColumn is null ? "NULL AS `date_creation`" : $"`{createdColumn}` AS `date_creation`")}
            FROM `{NotesTable}`
            WHERE itemtype = @itemtype
            """;

        Dictionary<int, (int Id, string Name)> users = await db.Users
            .Where(user => user.SourceGlpiId != null)
            .Select(user => new { Source = user.SourceGlpiId!.Value, user.Id, Name = user.DisplayName ?? user.UserName })
            .ToDictionaryAsync(user => user.Source, user => (user.Id, user.Name), ct);

        Dictionary<int, Notepad> existing = await db.Notepads
            .Where(note => note.SourceGlpiId != null)
            .ToDictionaryAsync(note => note.SourceGlpiId!.Value, ct);

        await using MySqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@itemtype", GlpiArticleItemType);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(ct);

        int seen = 0;

        while (await reader.ReadAsync(ct))
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.KnowledgeBaseNotes, ++seen));

            int sourceId = reader.GetInt32("id");
            int sourceArticleId = reader.GetInt32("items_id");

            if (!articleMap.TryGetValue(sourceArticleId, out int articleId))
            {
                // Case « Articles » décochée, ou article absent de la base source : la note n'a
                // personne à qui s'accrocher.
                result.NotesSkipped++;
                continue;
            }

            string? content = GetNullableString(reader, "content");

            if (content is null)
            {
                continue;
            }

            (int Id, string Name)? author = GetNullableInt(reader, "users_id") is int authorSourceId
                && users.TryGetValue(authorSourceId, out (int Id, string Name) match)
                    ? match
                    : null;

            string? lastEditorName = GetNullableInt(reader, "users_id_lastupdater") is int editorSourceId
                && users.TryGetValue(editorSourceId, out (int Id, string Name) editor)
                    ? editor.Name
                    : null;

            if (!existing.TryGetValue(sourceId, out Notepad? note))
            {
                note = new Notepad
                {
                    ItemType = ItemTypes.KnowledgeBaseArticle,
                    ItemId = articleId,
                    Content = content,
                    AuthorName = author?.Name ?? "Import GLPI",
                    SourceGlpiId = sourceId,
                    CreatedAt = GetNullableDate(reader, "date_creation") ?? DateTime.UtcNow,
                };

                db.Notepads.Add(note);
                existing[sourceId] = note;
            }
            else
            {
                note.ItemId = articleId;
                note.Content = content;
                note.AuthorName = author?.Name ?? note.AuthorName;
            }

            note.AuthorUserId = author?.Id;
            note.UpdatedAt = GetNullableDate(reader, "date_mod");
            note.LastEditorName = lastEditorName;

            result.NotesImported++;
        }

        await db.SaveChangesAsync(ct);

        if (result.NotesSkipped > 0)
        {
            result.Warnings.Add(
                $"{result.NotesSkipped} note(s) ignorée(s) : l'article auquel elles se rattachent n'a pas été "
                + "importé. Cochez « Articles » et relancez : une note ne peut se poser que sur un article déjà repris.");
        }
    }

    // ---- Aides --------------------------------------------------------------------------------

    /// <summary>Couple (identifiant GLPI d'origine, identifiant local) tel que lu en base.</summary>
    private sealed record SourceRef(int? SourceGlpiId, int LocalId);

    /// <summary>
    /// Construit la table (identifiant GLPI d'origine → identifiant local) à partir d'une requête
    /// déjà projetée.
    ///
    /// <paramref name="query"/> doit <b>déjà</b> avoir écarté les lignes sans identifiant d'origine
    /// (<c>Where(x =&gt; x.SourceGlpiId != null)</c> posé sur l'entité, avant le <c>Select</c>).
    /// Filtrer ici, après la projection, ne se traduit pas en SQL : EF ne sait pas relire un
    /// <see cref="SourceRef"/> construit dans la projection pour en extraire une colonne, et lève
    /// « The LINQ expression [...] could not be translated ». Le <c>!</c> ci-dessous s'appuie donc
    /// sur ce filtre amont, appliqué côté serveur — voir les appelants.
    /// </summary>
    private static async Task<Dictionary<int, int>> LoadSourceMapAsync(IQueryable<SourceRef> query, CancellationToken ct)
        => await query.ToDictionaryAsync(reference => reference.SourceGlpiId!.Value, reference => reference.LocalId, ct);

    private static int? ResolveEntity(MySqlDataReader reader, Dictionary<int, int> entityMap, int? rootEntityId)
        => GetNullableInt(reader, "entities_id") is int sourceEntityId && entityMap.TryGetValue(sourceEntityId, out int entityId)
            ? entityId
            : rootEntityId;

    private static string ColumnOrNull(HashSet<string> columns, string column)
        => columns.Contains(column) ? $"`{column}`" : $"NULL AS `{column}`";

    private static string? FirstPresent(HashSet<string> columns, params string[] candidates)
        => candidates.FirstOrDefault(columns.Contains);

    private static async Task<bool> TableExistsAsync(MySqlConnection connection, string table, CancellationToken ct)
        => (await GetColumnsAsync(connection, table, ct)).Count > 0;

    private static async Task<int> CountAsync(MySqlConnection connection, string table, CancellationToken ct)
    {
        if (!await TableExistsAsync(connection, table, ct))
        {
            return 0;
        }

        await using MySqlCommand command = new($"SELECT COUNT(*) FROM `{table}`", connection);
        object? value = await command.ExecuteScalarAsync(ct);

        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private static async Task<HashSet<string>> GetColumnsAsync(MySqlConnection connection, string table, CancellationToken ct)
    {
        HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
        const string sql = "SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @table";

        await using MySqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@table", table);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async IAsyncEnumerable<MySqlDataReader> ReadAsync(
        MySqlConnection connection, string sql, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using MySqlCommand command = new(sql, connection);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            yield return reader;
        }
    }

    private static string? GetNullableString(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        string value = reader.GetValue(ordinal).ToString() ?? string.Empty;

        // Toute chaîne venue de GLPI passe par le réparateur : les colonnes d'une base ancienne
        // sont déclarées latin1 tout en contenant de l'UTF-8, et les articles arrivent alors en
        // « ProcÃ©dure ». Fait ici plutôt qu'au cas par cas pour qu'aucune lecture n'y échappe —
        // sujets, contenus, noms de catégories, commentaires et révisions y passent tous.
        value = GlpiText.Repair(value) ?? string.Empty;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? GetNullableInt(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return int.TryParse(reader.GetValue(ordinal).ToString(), out int value) ? value : null;
    }

    /// <summary>
    /// Date MySQL, ramenée en UTC. GLPI écrit ses dates dans le fuseau du serveur ; GlpiNg stocke
    /// en UTC et convertit à l'affichage — sans cette conversion, tout l'historique importé serait
    /// décalé du décalage horaire du serveur source.
    /// </summary>
    private static DateTime? GetNullableDate(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        object value = reader.GetValue(ordinal);

        if (value is DateTime date)
        {
            return date == default ? null : DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
        }

        return DateTime.TryParse(value.ToString(), out DateTime parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime()
            : null;
    }
}
