namespace GlpiNg.Modules.Abstractions.Deployment;

/// <summary>
/// Implémenté par le module Déploiement (GlpiNg.Web) pour fournir à la fiche Ordinateur du
/// module Inventory le contenu de l'onglet "Tâches / groupes" (jobs de déploiement exécutés
/// sur l'agent du poste, groupes d'ordinateurs de déploiement dont il est membre) — reprend
/// l'onglet du même nom de GLPI-Inventory (PluginGlpiinventoryTaskjobstate). Suit le même
/// principe que <see cref="Menu.IMenuProvider"/> : le module Inventory dépend seulement de
/// cette abstraction, jamais du module Déploiement lui-même.
/// </summary>
public interface IComputerDeploymentTasksProvider
{
    Task<ComputerDeploymentTasksInfo> GetForComputerAsync(int computerId, CancellationToken cancellationToken = default);
}

public sealed class ComputerDeploymentTasksInfo
{
    public List<ComputerDeploymentTask> Tasks { get; set; } = [];
    public List<ComputerDeploymentGroup> Groups { get; set; } = [];
}

/// <summary>Un paquet de déploiement ayant ciblé ce poste, avec ses exécutions passées.</summary>
public sealed class ComputerDeploymentTask
{
    public int PackageId { get; set; }
    public required string Name { get; set; }
    public bool Active { get; set; }
    public required string MethodLabel { get; set; }
    public List<ComputerDeploymentTaskExecution> Executions { get; set; } = [];
}

public sealed class ComputerDeploymentTaskExecution
{
    public DateTime? DateUtc { get; set; }
    public required string StatusLabel { get; set; }
    public required string StatusBadgeCssClass { get; set; }
}

/// <summary>Un groupe d'ordinateurs de déploiement dont ce poste est membre (statique ou dynamique).</summary>
public sealed class ComputerDeploymentGroup
{
    public int GroupId { get; set; }
    public required string Name { get; set; }
    public required string TypeLabel { get; set; }
}
