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
    Task<GlpiPluginImportResult> RunAsync(
        string connectionString,
        string tablePrefix,
        GlpiPluginImportSelection selection,
        CancellationToken cancellationToken = default);
}

/// <summary>Catégories du plugin choisies par l'admin. Tout à <c>false</c> par défaut : rien n'est repris sans choix explicite.</summary>
public class GlpiPluginImportSelection
{
    public bool ImportIpRanges { get; set; }
    public bool ImportSnmpCredentials { get; set; }
    public bool ImportDeployPackages { get; set; }
    public bool ImportUnmanagedDevices { get; set; }

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

    /// <summary>Fichiers qu'un paquet importé référence sans que l'import puisse les rapatrier :
    /// ils vivent sur le disque du serveur GLPI et restent à téléverser à la main.</summary>
    public int DeployPackageFilesPending { get; set; }
    public int UnmanagedDevicesCreated { get; set; }
    public int UnmanagedDevicesUpdated { get; set; }

    public List<string> Warnings { get; set; } = [];
}
