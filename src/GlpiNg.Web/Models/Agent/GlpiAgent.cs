namespace GlpiNg.Web.Models.Agent;

/// <summary>
/// Représente un agent GLPI-Agent enregistré (identifié par sa deviceid).
/// </summary>
public class GlpiAgent
{
    public int Id { get; set; }

    /// <summary>Identifiant unique envoyé par l'agent (deviceid), ex: HOSTNAME-2024-01-01-12-00-00</summary>
    public required string DeviceId { get; set; }

    public string? Hostname { get; set; }
    public string? AgentVersion { get; set; }
    public string[] Tags { get; set; } = [];

    public DateTime FirstContactAt { get; set; } = DateTime.UtcNow;
    public DateTime LastContactAt { get; set; } = DateTime.UtcNow;

    public int? ComputerId { get; set; }
    public Computer? Computer { get; set; }

    public List<DeploymentJob> DeploymentJobs { get; set; } = [];
}
