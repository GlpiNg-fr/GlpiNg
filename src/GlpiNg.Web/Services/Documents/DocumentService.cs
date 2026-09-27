using GlpiNg.Modules.Abstractions.Documents;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Documents;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Documents;

/// <summary>
/// Les règles des documents : téléversement (avec déduplication), rattachement polymorphe,
/// recherche et suppression. Rend <see cref="IDocumentAttachments"/> aux modules, qui n'ont pas
/// accès au modèle de l'hôte.
///
/// Regroupées ici plutôt que dans chaque écran pour la même raison que les règles de la base de
/// connaissances : un document téléversé depuis un article et un document téléversé depuis
/// l'écran de gestion doivent produire exactement la même fiche.
/// </summary>
public sealed class DocumentService(
    IDbContextFactory<GlpiNgDbContext> dbFactory,
    DocumentStorageService storage) : IDocumentAttachments
{
    /// <inheritdoc />
    public string DownloadUrl(int documentId) => $"/documents/{documentId}/download";

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSummary>> GetForItemAsync(
        string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // IgnoreQueryFilters : la visibilité d'une pièce jointe suit l'objet auquel elle est
        // rattachée, pas sa propre entité — c'est la règle de GLPI, où l'on voit un document
        // parce qu'on voit l'article qui le porte. La frontière d'autorisation est donc l'objet,
        // et l'appelant l'a déjà franchie : la fiche vérifie la visibilité de l'article avant
        // d'afficher l'onglet.
        //
        // Sans cela, un document non récursif rangé dans une entité ancêtre disparaissait de
        // l'onglet d'un article pourtant visible, lui, parce que récursif — et sans le moindre
        // message, la ligne entière étant écartée par la jointure. C'est exactement ce que
        // produisait l'import : GLPI marque ses articles récursifs et ses documents non récursifs.
        return await db.DocumentItems
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(link => link.ItemType == itemType && link.ItemId == itemId)
            .OrderByDescending(link => link.AttachedAt)
            .Select(link => new DocumentSummary(
                link.Document!.Id,
                link.Document.Name,
                link.Document.FileName,
                link.Document.MimeType,
                link.Document.SizeBytes,
                link.Document.IsContentMissing,
                link.Document.CreatedAt,
                link.Document.AuthorName))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSummary>> SearchAsync(
        string? term, int limit = 20, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Document> query = db.Documents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            string needle = term.Trim();
            query = query.Where(document => document.Name.Contains(needle) || document.FileName.Contains(needle));
        }

        return await query
            .OrderByDescending(document => document.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(document => new DocumentSummary(
                document.Id,
                document.Name,
                document.FileName,
                document.MimeType,
                document.SizeBytes,
                document.IsContentMissing,
                document.CreatedAt,
                document.AuthorName))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> UploadAndAttachAsync(
        string itemType,
        int itemId,
        string fileName,
        Stream content,
        string? mimeType,
        string? authorName,
        int? authorUserId,
        CancellationToken cancellationToken = default)
    {
        int documentId = await UploadAsync(fileName, content, mimeType, authorName, authorUserId, null, cancellationToken);
        await AttachAsync(itemType, itemId, documentId, cancellationToken);

        return documentId;
    }

    /// <summary>
    /// Téléverse un fichier et renvoie l'identifiant du document.
    ///
    /// Si le contenu est déjà connu (même empreinte), aucune fiche n'est créée : l'existante est
    /// renvoyée. Téléverser deux fois le même mode d'emploi depuis deux articles ne doit pas
    /// donner deux documents que plus rien ne rapproche — c'est aussi ce qui rend un import
    /// répété inoffensif.
    /// </summary>
    public async Task<int> UploadAsync(
        string fileName,
        Stream content,
        string? mimeType,
        string? authorName,
        int? authorUserId,
        int? categoryId,
        CancellationToken cancellationToken = default)
    {
        (string sha256, long size, string storagePath) = await storage.SaveAsync(content, cancellationToken);

        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        Document? existing = await db.Documents
            .FirstOrDefaultAsync(document => document.Sha256 == sha256, cancellationToken);

        if (existing is not null)
        {
            // Une fiche importée sans son fichier retrouve son contenu si quelqu'un le téléverse
            // plus tard : c'est le chemin de réparation d'un import fait sans accès au dossier
            // files/ de GLPI.
            if (existing.IsContentMissing)
            {
                existing.IsContentMissing = false;
                existing.StoragePath = storagePath;
                existing.SizeBytes = size;
                await db.SaveChangesAsync(cancellationToken);
            }

            return existing.Id;
        }

        Document created = new()
        {
            Name = Path.GetFileNameWithoutExtension(fileName) is { Length: > 0 } stem ? stem : fileName,
            FileName = fileName,
            MimeType = mimeType,
            Sha256 = sha256,
            StoragePath = storagePath,
            SizeBytes = size,
            CategoryId = categoryId,
            AuthorName = authorName,
            AuthorUserId = authorUserId,
            CreatedAt = DateTime.UtcNow,
        };

        db.Documents.Add(created);
        await db.SaveChangesAsync(cancellationToken);

        return created.Id;
    }

    /// <inheritdoc />
    public async Task AttachAsync(string itemType, int itemId, int documentId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        bool already = await db.DocumentItems.AnyAsync(
            link => link.DocumentId == documentId && link.ItemType == itemType && link.ItemId == itemId,
            cancellationToken);

        if (already)
        {
            return;
        }

        db.DocumentItems.Add(new DocumentItem
        {
            DocumentId = documentId,
            ItemType = itemType,
            ItemId = itemId,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DetachAsync(string itemType, int itemId, int documentId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        await db.DocumentItems
            .Where(link => link.DocumentId == documentId && link.ItemType == itemType && link.ItemId == itemId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Le document désigné, pour le téléchargement.
    ///
    /// Cherché d'abord dans le périmètre d'entité du lecteur, puis — à défaut — parmi les
    /// documents <b>rattachés à au moins un objet</b>. Ce second essai est le pendant de
    /// <see cref="GetForItemAsync"/> : un document listé dans l'onglet d'un article doit pouvoir
    /// être téléchargé, sans quoi l'écran proposerait un lien qui répond 404.
    ///
    /// Un document rattaché nulle part, lui, reste cloisonné : il n'apparaît que dans l'écran de
    /// gestion, où le périmètre d'entité fait foi.
    /// </summary>
    public async Task<Document?> GetAsync(int documentId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        Document? scoped = await db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == documentId, cancellationToken);

        return scoped ?? await db.Documents
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(document => document.Id == documentId && document.Items.Count > 0, cancellationToken);
    }

    /// <summary>Ouvre le contenu d'un document, ou <c>null</c> si la fiche n'a pas de fichier.</summary>
    public Stream? OpenContent(Document document)
        => document.IsContentMissing ? null : storage.OpenRead(document.StoragePath);

    /// <summary>
    /// Supprime la fiche, ses rattachements, et le fichier <b>si plus aucune autre fiche ne le
    /// partage</b> : le rangement par empreinte fait que deux documents de même contenu pointent
    /// sur le même fichier, et l'effacer sans ce contrôle viderait l'autre.
    /// </summary>
    public async Task DeleteAsync(int documentId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        Document? document = await db.Documents.FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken);

        if (document is null)
        {
            return;
        }

        string storagePath = document.StoragePath;
        bool hadContent = !document.IsContentMissing;

        // Les rattachements partent par cascade (voir la configuration du DbContext).
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);

        bool stillReferenced = await db.Documents
            .AnyAsync(item => item.StoragePath == storagePath, cancellationToken);

        if (hadContent && !stillReferenced)
        {
            storage.Delete(storagePath);
        }
    }

    // La projection vers DocumentSummary est écrite à la main dans chaque requête plutôt que
    // sortie dans une méthode partagée : EF ne sait pas traduire un appel de méthode dans un
    // Select, et la factorisation se paierait d'un « The LINQ expression could not be translated »
    // à l'exécution — même piège que dans l'import de la base de connaissances.
}
