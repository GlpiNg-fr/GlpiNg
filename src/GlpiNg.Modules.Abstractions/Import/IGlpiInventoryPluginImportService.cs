namespace GlpiNg.Modules.Abstractions.Import;

/// <summary>
/// Importe les données du plugin d'inventaire de la base GLPI source (GLPI Inventory ou
/// FusionInventory) vers leurs équivalents GlpiNg. Implémenté par l'hôte, comme
/// <see cref="IGlpiAdminImportService"/> et pour la même raison : les cibles (plages IP,
/// identifiants SNMP, paquets de déploiement, actifs non gérés) appartiennent au module
/// Déploiement, que le module Inventory — qui pilote la page <c>/admin/import/glpi</c> — ne
/// référence pas.
///
/// Le préfixe des tables est fourni par l'appelant plutôt que redéduit ici : il vient de la
/// détection faite pendant l'analyse (voir
/// <c>GlpiNg.Modules.Inventory.Import.GlpiInventoryPluginInfo</c>), qui sait lequel des deux
/// plugins est en place.
/// </summary>
public interface IGlpiInventoryPluginImportService
{
    // « progress » : rapporteur d'avancement, optionnel. Ces catégories portent peu de lignes au
    // regard du parc — l'annonce se fait à l'entrée de chaque phase, sans compter les éléments un
    // à un.
    Task<GlpiPluginImportResult> RunAsync(
        string connectionString,
        string tablePrefix,
        GlpiPluginImportSelection selection,
        IProgress<GlpiImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Catégories du plugin choisies par l'admin. Tout à <c>false</c> par défaut : rien n'est repris sans choix explicite.</summary>
public class GlpiPluginImportSelection
{
    public bool ImportIpRanges { get; set; }
    public bool ImportSnmpCredentials { get; set; }
    public bool ImportDeployPackages { get; set; }
    public bool ImportUnmanagedDevices { get; set; }

    /// <summary>
    /// Répertoire des fichiers du plugin GLPI, vu depuis la machine GlpiNg (chemin local ou partage
    /// réseau). Renseigné, l'import ne se contente pas de déclarer les fichiers d'un paquet : il en
    /// rapatrie le contenu.
    /// </summary>
    public string? DeployFilesPath { get; set; }

    /// <summary>Compte à présenter au partage réseau hébergeant <see cref="DeployFilesPath"/>.
    /// Optionnel : sans lui, la lecture se fait avec le compte du processus.</summary>
    public string? DeployFilesUserName { get; set; }

    /// <summary>Mot de passe associé à <see cref="DeployFilesUserName"/>.</summary>
    public string? DeployFilesPassword { get; set; }

    /// <summary>Racine HTTP de GLPI, essayée quand le répertoire n'est pas renseigné ou ne rend rien.</summary>
    public string? GlpiBaseUrl { get; set; }

    /// <summary>Serveurs de miroir déclarés par le plugin sur la base source : ce sont les adresses
    /// depuis lesquelles ses agents téléchargent réellement, donc les premières à essayer.</summary>
    public IReadOnlyList<string> DeployMirrorUrls { get; set; } = [];

    public bool AnySelected => ImportIpRanges || ImportSnmpCredentials || ImportDeployPackages || ImportUnmanagedDevices;
}

/// <summary>Résumé d'une exécution de l'import des données du plugin d'inventaire.</summary>
public class GlpiPluginImportResult
{
    public int IpRangesCreated { get; set; }
    public int IpRangesUpdated { get; set; }
    public int SnmpCredentialsCreated { get; set; }
    public int SnmpCredentialsUpdated { get; set; }
    public int DeployPackagesCreated { get; set; }
    public int DeployPackagesUpdated { get; set; }

    /// <summary>Vérifications reprises depuis le contenu JSON des paquets.</summary>
    public int DeployPackageChecksImported { get; set; }

    /// <summary>Actions reprises depuis le contenu JSON des paquets.</summary>
    public int DeployPackageActionsImported { get; set; }

    public int DeployPackageFilesCreated { get; set; }
    public int DeployPackageFilesUpdated { get; set; }

    /// <summary>Fichiers dont le contenu a pu être rapatrié depuis l'installation GLPI source.</summary>
    public int DeployPackageFilesDownloaded { get; set; }

    /// <summary>Fichiers créés sans leur contenu : les octets vivent sur le disque du serveur GLPI
    /// et restent à téléverser à la main depuis la fiche du paquet.</summary>
    public int DeployPackageFilesPending { get; set; }
    public int UnmanagedDevicesCreated { get; set; }
    public int UnmanagedDevicesUpdated { get; set; }

    public List<string> Warnings { get; set; } = [];
}
