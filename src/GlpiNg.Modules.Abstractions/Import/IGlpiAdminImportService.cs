namespace GlpiNg.Modules.Abstractions.Import;

/// <summary>
/// Implémenté par l'hôte (GlpiNg.Web) pour importer les données "Administration" (entités,
/// groupes, profils, utilisateurs) et la configuration générale depuis une base GLPI MySQL
/// source, sans que le module Inventory (qui pilote la page <c>/admin/import/glpi</c> via
/// <see cref="object"/> — voir GlpiNg.Modules.Inventory.Import.GlpiImportStateService) ne
/// dépende de GlpiEntity/GlpiGroup/GlpiProfile/GlpiUser (domaine utilisateurs, propriété de
/// l'hôte) — même principe qu'<c>ICurrentUserDeploymentContextProvider</c> pour le module
/// Déploiement. La connexion réutilise la même base source que
/// <c>GlpiMySqlImportService</c> (module Inventory) : les deux imports (parc et
/// administration) ciblent la même installation GLPI, juste des tables différentes.
/// </summary>
public interface IGlpiAdminImportService
{
    /// <summary>Compte, sans rien importer, les éléments disponibles par catégorie dans la base GLPI MySQL source.</summary>
    Task<GlpiAdminImportAnalysis> AnalyzeAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Importe les catégories cochées dans <paramref name="selection"/> depuis la base GLPI MySQL source.</summary>
    // « progress » : rapporteur d'avancement, optionnel. Ces catégories portent peu de lignes au
    // regard du parc — l'annonce se fait à l'entrée de chaque phase, sans compter les éléments un
    // à un.
    Task<GlpiAdminImportResult> RunAsync(
        string connectionString,
        GlpiAdminImportSelection selection,
        IProgress<GlpiImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Résumé, avant import, du contenu "Administration"/"Configuration" de la base GLPI MySQL source.</summary>
public class GlpiAdminImportAnalysis
{
    public int EntitiesCount { get; set; }
    public int GroupsCount { get; set; }
    public int ProfilesCount { get; set; }
    public int UsersCount { get; set; }

    /// <summary>Vrai si la base source expose au moins une clé de configuration générale (glpi_configs, context "core").</summary>
    public bool GeneralConfigAvailable { get; set; }
}

/// <summary>Catégories "Administration"/"Configuration" choisies par l'admin pour un import GLPI MySQL.</summary>
public class GlpiAdminImportSelection
{
    public bool ImportEntities { get; set; } = true;
    public bool ImportGroups { get; set; } = true;
    public bool ImportProfiles { get; set; } = true;
    public bool ImportUsers { get; set; } = true;
    public bool ImportGeneralConfig { get; set; } = true;

    public bool AnySelected =>
        ImportEntities || ImportGroups || ImportProfiles || ImportUsers || ImportGeneralConfig;
}

/// <summary>Résumé d'une exécution de l'import GLPI "Administration"/"Configuration".</summary>
public class GlpiAdminImportResult
{
    public int EntitiesCreated { get; set; }
    public int EntitiesUpdated { get; set; }
    public int GroupsCreated { get; set; }
    public int GroupsUpdated { get; set; }
    public int ProfilesCreated { get; set; }
    public int ProfilesUpdated { get; set; }
    public int UsersCreated { get; set; }
    public int UsersUpdated { get; set; }

    /// <summary>Nombre de clés de configuration générale (glpi_configs) reconnues et appliquées à GeneralSettings.</summary>
    public int GeneralConfigKeysImported { get; set; }

    public List<string> Warnings { get; set; } = [];
}
