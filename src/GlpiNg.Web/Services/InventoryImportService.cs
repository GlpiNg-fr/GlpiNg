using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Agent;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Importe le contenu d'une requête "inventory" du protocole GLPI-Agent
/// dans les entités Computer / ComputerComponent, en le rattachant à l'agent fourni.
/// </summary>
public class InventoryImportService(GlpiNgDbContext db)
{
    public async Task<Computer> ImportAsync(GlpiAgent agent, InventoryContent content, CancellationToken cancellationToken = default)
    {
        Computer? computer = await db.Computers
            .Include(c => c.Components)
            .FirstOrDefaultAsync(c => c.AgentId == agent.Id, cancellationToken);

        // Fallback de corrélation : si l'agent n'est pas encore lié, on tente de retrouver
        // le poste par son UUID matériel (stable même si le nom de machine change).
        if (computer is null && content.Hardware?.Uuid is { Length: > 0 } uuid)
        {
            computer = await db.Computers.Include(c => c.Components)
                .FirstOrDefaultAsync(c => c.HardwareUuid == uuid, cancellationToken);
        }

        if (computer is null)
        {
            computer = new Computer { Name = content.Hardware?.Name ?? agent.Hostname ?? agent.DeviceId ?? "Inconnu" };
            db.Computers.Add(computer);
        }

        ApplyHardware(computer, content);
        ApplyComponents(computer, content);

        computer.LastInventoryAt = DateTime.UtcNow;
        computer.Status = ComputerStatus.InProduction;

        agent.Computer = computer;

        await db.SaveChangesAsync(cancellationToken);
        return computer;
    }

    private static void ApplyHardware(Computer computer, InventoryContent content)
    {
        if (content.Hardware is { } hardware)
        {
            computer.Name = string.IsNullOrWhiteSpace(hardware.Name) ? computer.Name : hardware.Name;
            computer.HardwareUuid = hardware.Uuid ?? computer.HardwareUuid;
            computer.ChassisType = hardware.ChassisType ?? computer.ChassisType;
            computer.TotalMemoryMb = hardware.MemoryMb ?? computer.TotalMemoryMb;
        }

        if (content.Bios is { } bios)
        {
            computer.Manufacturer = bios.SystemManufacturer ?? computer.Manufacturer;
            computer.Model = bios.SystemModel ?? computer.Model;
            computer.SerialNumber = bios.SystemSerial ?? computer.SerialNumber;
        }

        if (content.OperatingSystem is { } os)
        {
            computer.OperatingSystem = os.FullName ?? os.Name ?? computer.OperatingSystem;
            computer.OsVersion = os.Version ?? computer.OsVersion;
        }
    }

    /// <summary>
    /// Remplace intégralement les composants du poste par ceux de l'inventaire courant.
    /// Choix volontaire : un inventaire GLPI-Agent est toujours un instantané complet,
    /// donc "remplacer" reflète mieux la réalité qu'un merge incrémental fragile.
    /// </summary>
    private static void ApplyComponents(Computer computer, InventoryContent content)
    {
        computer.Components.Clear();

        foreach (InventoryCpu cpu in content.Cpus)
        {
            computer.Components.Add(new ComputerComponent
            {
                Type = ComponentType.Cpu,
                Designation = cpu.Name ?? cpu.Manufacturer ?? "CPU inconnu",
                Capacity = cpu.SpeedMhz is { } mhz ? $"{mhz} MHz / {cpu.Cores ?? 0} coeurs" : null,
                Serial = cpu.Serial
            });
        }

        foreach (InventoryMemory memory in content.Memories)
        {
            // Un slot vide (pas de capacité) n'est pas un module physique installé — on l'ignore.
            if (memory.CapacityMb is null or 0) continue;

            computer.Components.Add(new ComputerComponent
            {
                Type = ComponentType.Ram,
                Designation = memory.Description ?? memory.Caption ?? "Barrette mémoire",
                Capacity = $"{memory.CapacityMb} Mo",
                Serial = memory.Serial
            });
        }

        foreach (InventoryStorage storage in content.Storages)
        {
            computer.Components.Add(new ComputerComponent
            {
                Type = ComponentType.Disk,
                Designation = storage.Model ?? storage.Name ?? "Disque inconnu",
                Capacity = storage.DiskSizeMb is { } mb ? $"{mb} Mo" : null,
                Serial = storage.Serial
            });
        }

        foreach (InventoryNetwork network in content.Networks)
        {
            computer.Components.Add(new ComputerComponent
            {
                Type = ComponentType.NetworkCard,
                Designation = network.Description ?? "Carte réseau",
                Capacity = null,
                Serial = network.MacAddress
            });
        }
    }
}
