namespace GlpiNg.Modules.Abstractions.Deployment;

/// <summary>
/// Implémenté par le module Déploiement pour alimenter l'onglet « Informations de collecte » de la
/// fiche Ordinateur : ce que les collectes (clés de registre, requêtes WMI, recherches de fichiers)
/// ont rapporté de ce poste. Même montage que
/// <see cref="IComputerDeploymentTasksProvider"/> — le module Inventory ne dépend que du contrat.
/// </summary>
public interface IComputerCollectProvider
{
    Task<ComputerCollectInfo> GetForComputerAsync(int computerId, CancellationToken cancellationToken = default);
}

/// <summary>Ce qu'on sait des collectes pour un poste : ce qui a été rapporté, et ce qui est attendu.</summary>
public sealed class ComputerCollectInfo
{
    public List<ComputerCollectGroup> Groups { get; set; } = [];

    /// <summary>
    /// Collectes actives qui n'ont encore rien rapporté pour ce poste.
    ///
    /// Distinguées d'une absence pure : une collecte définie mais jamais remontée signale un agent
    /// qui ne l'a pas encore exécutée, là où l'absence de toute collecte signale qu'il n'y a rien à
    /// attendre. Les deux se ressemblent à l'écran si on ne les sépare pas.
    /// </summary>
    public List<string> PendingCollects { get; set; } = [];
}

/// <summary>Les résultats d'une collecte pour ce poste.</summary>
public sealed class ComputerCollectGroup
{
    public int CollectId { get; set; }
    public required string Name { get; set; }
    public required string TypeLabel { get; set; }
    public DateTime? LastCollectedAt { get; set; }
    public List<ComputerCollectEntry> Entries { get; set; } = [];
}

/// <summary>Une valeur rapportée : le nom de l'entrée interrogée, ce qui a été demandé, ce qui a été trouvé.</summary>
public sealed class ComputerCollectEntry
{
    public required string EntryName { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public DateTime CollectedAt { get; set; }
}
