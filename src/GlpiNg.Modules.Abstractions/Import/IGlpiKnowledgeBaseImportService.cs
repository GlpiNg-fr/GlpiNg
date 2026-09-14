namespace GlpiNg.Modules.Abstractions.Import;

/// <summary>
/// Importe la base de connaissances de la base GLPI source (catégories, articles, cibles de
/// visibilité, révisions, documents) vers leurs équivalents GlpiNg. Implémenté par l'hôte, comme
/// <see cref="IGlpiAdminImportService"/> et <see cref="IGlpiInventoryPluginImportService"/>, et
/// pour la même raison : les cibles appartiennent au module Base de connaissances et les entités,
/// groupes, profils et comptes auxquels elles renvoient appartiennent à l'hôte — le module
/// Inventory, qui pilote la page <c>/admin/import/glpi</c>, ne référence ni l'un ni l'autre.
///
/// Lecture seule sur la base source et idempotent par <c>SourceGlpiId</c>, comme le reste de
/// l'import : relancer met à jour, ne duplique pas.
/// </summary>
public interface IGlpiKnowledgeBaseImportService
{
    /// <summary>Ce que la base source contient, pour afficher des cases à cocher chiffrées plutôt
    /// qu'à l'aveugle.</summary>
    Task<GlpiKnowledgeBaseImportAnalysis> AnalyzeAsync(string connectionString, CancellationToken cancellationToken = default);

    Task<GlpiKnowledgeBaseImportResult> RunAsync(
        string connectionString,
        GlpiKnowledgeBaseImportSelection selection,
        IProgress<GlpiImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Volume de la base de connaissances de la base source, par catégorie d'objet.</summary>
public class GlpiKnowledgeBaseImportAnalysis
{
    public int CategoriesCount { get; set; }
    public int ArticlesCount { get; set; }

    /// <summary>Lignes des quatre tables de visibilité (utilisateurs, groupes, profils, entités) cumulées.</summary>
    public int TargetsCount { get; set; }

    /// <summary>Révisions archivées par GLPI (table présente à partir de GLPI 9.2).</summary>
    public int RevisionsCount { get; set; }

    /// <summary>Documents rattachés à un article (<c>glpi_documents_items</c> en itemtype KnowbaseItem).</summary>
    public int DocumentsCount { get; set; }

    /// <summary>Vrai si la base source porte au moins la table des articles : sans elle, il n'y a
    /// rien à proposer, et l'écran le dit plutôt que d'afficher des zéros.</summary>
    public bool IsPresent { get; set; }
}

/// <summary>Catégories choisies par l'admin pour l'import de la base de connaissances.</summary>
public class GlpiKnowledgeBaseImportSelection
{
    public bool ImportCategories { get; set; }
    public bool ImportArticles { get; set; }

    /// <summary>
    /// Cibles de visibilité. Sans elles, un article restreint côté GLPI arriverait visible de tous :
    /// c'est pour cette raison que la case est proposée à part, et non fondue dans « Articles ».
    /// </summary>
    public bool ImportTargets { get; set; }

    /// <summary>Historique des modifications archivé par GLPI.</summary>
    public bool ImportRevisions { get; set; }

    /// <summary>Documents rattachés aux articles, et leurs catégories.</summary>
    public bool ImportDocuments { get; set; }

    /// <summary>
    /// Dossier <c>files/</c> de l'installation GLPI source (chemin local ou partage réseau), d'où
    /// lire le contenu des documents.
    ///
    /// Facultatif, et c'est délibéré : GLPI ne range pas les fichiers en base — seul le chemin
    /// relatif y figure (<c>glpi_documents.filepath</c>) — donc la connexion MySQL seule ne peut
    /// ramener que des métadonnées. Laissé vide, l'import crée les fiches et les rattachements
    /// sans contenu téléchargeable, ce que l'écran signale ; renseigné, il copie les fichiers dans
    /// la racine de stockage de GlpiNg.
    /// </summary>
    public string? GlpiFilesPath { get; set; }

    public bool AnySelected => ImportCategories || ImportArticles || ImportTargets || ImportRevisions || ImportDocuments;
}

/// <summary>Résumé d'une exécution de l'import de la base de connaissances.</summary>
public class GlpiKnowledgeBaseImportResult
{
    public int CategoriesCreated { get; set; }
    public int CategoriesUpdated { get; set; }
    public int ArticlesCreated { get; set; }
    public int ArticlesUpdated { get; set; }
    public int TargetsImported { get; set; }
    public int RevisionsImported { get; set; }

    /// <summary>Documents créés, avec leur fichier.</summary>
    public int DocumentsImported { get; set; }

    /// <summary>Rattachements document → article créés.</summary>
    public int DocumentLinksImported { get; set; }

    /// <summary>
    /// Documents repris en métadonnées seules, faute d'avoir pu lire le fichier (dossier
    /// <c>files/</c> non renseigné, inaccessible, ou fichier absent). Comptés à part parce que la
    /// fiche existe et paraît normale : sans ce chiffre, on croirait l'import complet.
    /// </summary>
    public int DocumentsWithoutContent { get; set; }

    /// <summary>
    /// Cibles laissées de côté faute d'avoir retrouvé l'objet visé côté GlpiNg (groupe, profil ou
    /// compte non importé). Comptées à part : un article qui perd une cible devient plus visible
    /// qu'il ne l'était, ce que l'administrateur doit savoir.
    /// </summary>
    public int TargetsSkipped { get; set; }

    public List<string> Warnings { get; set; } = [];
}
