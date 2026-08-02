using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
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
            .Include(c => c.Softwares)
            .Include(c => c.Peripherals)
            .Include(c => c.Volumes)
            .Include(c => c.Batteries)
            .Include(c => c.NetworkPorts)
            .FirstOrDefaultAsync(c => c.AgentId == agent.Id, cancellationToken);

        // Fallback de corrélation : si l'agent n'est pas encore lié, on tente de retrouver
        // le poste par son UUID matériel (stable même si le nom de machine change).
        if (computer is null && content.Hardware?.Uuid is { Length: > 0 } uuid)
        {
            computer = await db.Computers.Include(c => c.Components)
                .Include(c => c.Softwares)
                .Include(c => c.Peripherals)
                .Include(c => c.Volumes)
                .Include(c => c.Batteries)
                .Include(c => c.NetworkPorts)
                .FirstOrDefaultAsync(c => c.HardwareUuid == uuid, cancellationToken);
        }

        if (computer is null)
        {
            computer = new Computer { Name = content.Hardware?.Name ?? agent.Hostname ?? agent.DeviceId ?? "Inconnu" };
            db.Computers.Add(computer);
        }

        ApplyHardware(computer, content);
        ApplyComponents(computer, content);
        ApplySoftwares(computer, content);
        ApplyMonitors(computer, content);
        ApplyVolumes(computer, content);
        ApplyBatteries(computer, content);
        ApplyNetworkPorts(computer, content);

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
            computer.LastLoggedUser = hardware.LastLoggedUser ?? computer.LastLoggedUser;
            computer.VmSystem = hardware.VmSystem ?? computer.VmSystem;
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
            computer.OsKernelVersion = os.KernelVersion ?? computer.OsKernelVersion;
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

    /// <summary>
    /// Remplace intégralement les logiciels du poste par ceux de l'inventaire courant, pour
    /// la même raison que <see cref="ApplyComponents"/> : un inventaire GLPI-Agent est un
    /// instantané complet des logiciels installés.
    /// </summary>
    private static void ApplySoftwares(Computer computer, InventoryContent content)
    {
        computer.Softwares.Clear();

        foreach (InventorySoftware software in content.Softwares)
        {
            if (string.IsNullOrWhiteSpace(software.Name)) continue;

            computer.Softwares.Add(new ComputerSoftware
            {
                Name = software.Name,
                Version = software.Version,
                Publisher = software.Publisher,
                InstallDate = software.InstallDate
            });
        }
    }

    /// <summary>
    /// Remplace les moniteurs (<see cref="PeripheralKind.Monitor"/>) du poste par ceux de
    /// l'inventaire courant, même logique d'instantané complet que <see cref="ApplyComponents"/>.
    /// Ne touche pas aux autres natures de <see cref="ComputerPeripheral"/> (imprimantes, etc.),
    /// qui ne sont pas encore alimentées par l'inventaire.
    /// </summary>
    private static void ApplyMonitors(Computer computer, InventoryContent content)
    {
        computer.Peripherals.RemoveAll(peripheral => peripheral.Kind == PeripheralKind.Monitor);

        foreach (InventoryMonitor monitor in content.Monitors)
        {
            string designation = monitor.Caption ?? monitor.Description ?? "Écran inconnu";

            computer.Peripherals.Add(new ComputerPeripheral
            {
                Kind = PeripheralKind.Monitor,
                Designation = designation,
                Manufacturer = monitor.Manufacturer,
                Serial = monitor.Serial
            });
        }
    }

    /// <summary>
    /// Remplace intégralement les volumes du poste par ceux de l'inventaire courant, même
    /// logique d'instantané complet que <see cref="ApplyComponents"/>.
    /// </summary>
    private static void ApplyVolumes(Computer computer, InventoryContent content)
    {
        computer.Volumes.Clear();

        foreach (InventoryDrive drive in content.Drives)
        {
            string name = drive.Volume ?? drive.Label ?? drive.Letter ?? "Volume inconnu";

            computer.Volumes.Add(new ComputerVolume
            {
                Name = name,
                Partition = drive.Type,
                MountPoint = drive.Letter ?? drive.Volume,
                FileSystem = drive.FileSystem,
                TotalSizeMb = drive.TotalMb,
                FreeSizeMb = drive.FreeMb
            });
        }
    }

    /// <summary>
    /// Remplace intégralement les batteries du poste par celles de l'inventaire courant, même
    /// logique d'instantané complet que <see cref="ApplyComponents"/>.
    /// </summary>
    private static void ApplyBatteries(Computer computer, InventoryContent content)
    {
        computer.Batteries.Clear();

        foreach (InventoryBattery battery in content.Batteries)
        {
            computer.Batteries.Add(new ComputerBattery
            {
                Name = battery.Name ?? battery.Manufacturer ?? "Batterie inconnue",
                Manufacturer = battery.Manufacturer,
                Serial = battery.Serial,
                Chemistry = battery.Chemistry,
                VoltageMv = battery.VoltageMv,
                CapacityMwh = battery.CapacityMwh,
                ManufactureDate = battery.ManufactureDate
            });
        }
    }

    /// <summary>
    /// Remplace intégralement les ports réseau du poste par ceux de l'inventaire courant, même
    /// logique d'instantané complet que <see cref="ApplyComponents"/>. Distinct de
    /// <see cref="ApplyComponents"/> (qui liste les cartes réseau comme composants matériel) :
    /// ici on conserve la configuration IP/logique de chaque interface, à l'instar de l'onglet
    /// "Ports réseau" de GLPI.
    /// </summary>
    private static void ApplyNetworkPorts(Computer computer, InventoryContent content)
    {
        computer.NetworkPorts.Clear();

        foreach (InventoryNetwork network in content.Networks)
        {
            computer.NetworkPorts.Add(new ComputerNetworkPort
            {
                Designation = network.Description ?? "Interface réseau",
                Type = network.Type,
                MacAddress = network.MacAddress,
                Manufacturer = network.Manufacturer,
                IpAddress = network.IpAddress,
                IpMask = network.IpMask,
                IpGateway = network.IpGateway,
                IpSubnet = network.IpSubnet,
                IpDhcp = network.IpDhcp,
                Mtu = network.Mtu,
                SpeedMbps = network.SpeedMbps,
                Status = network.Status,
                IsVirtual = network.VirtualDevice == "1"
            });
        }
    }
}
