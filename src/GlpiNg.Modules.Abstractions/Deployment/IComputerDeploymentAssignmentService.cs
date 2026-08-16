namespace GlpiNg.Modules.Abstractions.Deployment;

/// <summary>
/// Implémenté par le module Déploiement pour permettre à la fiche Ordinateur du module
/// Inventory (onglet "Déploiement de package") de lister les paquets disponibles, d'assigner
/// un paquet à l'agent du poste (création d'un <c>DeploymentJob</c>) et de suivre/annuler les
/// assignations en cours. Même principe que <see cref="IComputerDeploymentTasksProvider"/> :
/// le module Inventory dépend seulement de cette abstraction, jamais du module Déploiement.
/// </summary>
public interface IComputerDeploymentAssignmentService
{
    Task<List<DeploymentPackageOption>> GetAvailablePackagesAsync(CancellationToken cancellationToken = default);

    Task<List<ComputerDeploymentAssignment>> GetAssignmentsAsync(int computerId, CancellationToken cancellationToken = default);

    Task<DeploymentAssignmentResult> AssignPackagesAsync(int computerId, IReadOnlyCollection<int> packageIds, CancellationToken cancellationToken = default);

    Task<bool> CancelAssignmentAsync(int jobId, CancellationToken cancellationToken = default);
}

/// <summary>Un paquet de déploiement pouvant être assigné (non remplacé par un autre paquet).</summary>
public sealed class DeploymentPackageOption
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

/// <summary>Une assignation (job de déploiement) du paquet à l'agent du poste.</summary>
public sealed class ComputerDeploymentAssignment
{
    public int JobId { get; set; }
    public int PackageId { get; set; }
    public required string PackageName { get; set; }
    public required string StatusLabel { get; set; }
    public required string StatusBadgeCssClass { get; set; }
    public bool CanCancel { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public enum DeploymentAssignmentStatus
{
    Success,
    NoAgent,
    PackageNotFound
}

public sealed class DeploymentAssignmentResult
{
    public DeploymentAssignmentStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}
