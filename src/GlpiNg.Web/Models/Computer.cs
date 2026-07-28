namespace GlpiNg.Web.Models;

public enum ComputerStatus
{
    InStock,
    InProduction,
    Broken,
    Retired
}

public class Computer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? OperatingSystem { get; set; }
    public string? OsVersion { get; set; }

    public ComputerStatus Status { get; set; } = ComputerStatus.InStock;

    public string? Site { get; set; }
    public string? Building { get; set; }
    public string? Room { get; set; }

    public string? AssignedUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastInventoryAt { get; set; }

    public List<ComputerComponent> Components { get; set; } = [];

    // Lien vers l'agent GLPI qui remonte les infos pour cette machine
    public int? AgentId { get; set; }
    public Agent.GlpiAgent? Agent { get; set; }
}

public enum ComponentType
{
    Cpu,
    Ram,
    Disk,
    NetworkCard,
    Gpu,
    Motherboard
}

public class ComputerComponent
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    public ComponentType Type { get; set; }
    public required string Designation { get; set; }
    public string? Capacity { get; set; }
    public string? Serial { get; set; }
}
