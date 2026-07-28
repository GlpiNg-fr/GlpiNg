namespace GlpiNg.Web.Models.Agent;

public enum DeploymentStatus
{
    Pending,
    Running,
    Success,
    Error
}

/// <summary>
/// Job de déploiement de package pour un agent donné (tâche "deploy" du protocole GLPI-Agent).
/// </summary>
public class DeploymentJob
{
    public int Id { get; set; }

    public int AgentId { get; set; }
    public GlpiAgent? Agent { get; set; }

    public int PackageId { get; set; }
    public DeploymentPackage? Package { get; set; }

    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Journal brut renvoyé par l'agent en fin d'exécution.</summary>
    public string? Log { get; set; }
}

/// <summary>
/// Package de déploiement : fichiers (avec hash SHA512) + actions (cmd, move, copy, delete, mkdir).
/// </summary>
public class DeploymentPackage
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<DeploymentPackageFile> Files { get; set; } = [];

    /// <summary>Liste d'actions sérialisées en JSON (format compatible protocole agent).</summary>
    public string ActionsJson { get; set; } = "[]";
}

public class DeploymentPackageFile
{
    public int Id { get; set; }
    public int DeploymentPackageId { get; set; }
    public required string FileName { get; set; }
    public required string Sha512 { get; set; }
    public long SizeBytes { get; set; }
    public required string StoragePath { get; set; }
}
