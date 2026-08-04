using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Agent;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Importe le contenu d'une requête "inventory" du protocole GLPI-Agent
/// dans les entités Computer / ComputerComponent, en le rattachant à l'agent fourni.
/// </summary>
public class InventoryImportService(GlpiNgDbContext db, AppSettingsFileStore settingsStore)
{
    private const string HistoryUser = "inventory";
    private const string SettingsSection = "InventorySettings";

    public async Task<Computer> ImportAsync(GlpiAgent agent, InventoryContent content, CancellationToken cancellationToken = default)
    {
        InventorySettings settings = await settingsStore.ReadSectionAsync<InventorySettings>(SettingsSection, cancellationToken);

        Computer? computer = await db.Computers
            .Include(c => c.Components)
            .Include(c => c.Softwares)
            .Include(c => c.Peripherals)
            .Include(c => c.Volumes)
            .Include(c => c.Batteries)
            .Include(c => c.NetworkPorts)
            .FirstOrDefaultAsync(c => c.AgentId == agent.Id, cancellationToken);

        string ruleName;
        string? inputValue;
        bool isNew = false;

        if (computer is not null)
        {
            ruleName = "Mise à jour de l'ordinateur (agent associé)";
            inputValue = $"agent #{agent.Id}";
        }
        // Fallback de corrélation : si l'agent n'est pas encore lié, on tente de retrouver
        // le poste par son UUID matériel (stable même si le nom de machine change).
        else if (content.Hardware?.Uuid is { Length: > 0 } uuid && (computer = await db.Computers
                     .Include(c => c.Components)
                     .Include(c => c.Softwares)
                     .Include(c => c.Peripherals)
                     .Include(c => c.Volumes)
                     .Include(c => c.Batteries)
                     .Include(c => c.NetworkPorts)
                     .FirstOrDefaultAsync(c => c.HardwareUuid == uuid, cancellationToken)) is not null)
        {
            ruleName = "Mise à jour de l'ordinateur (par UUID matériel)";
            inputValue = uuid;
        }
        else
        {
            computer = new Computer { Name = content.Hardware?.Name ?? agent.Hostname ?? agent.DeviceId ?? "Inconnu" };
            db.Computers.Add(computer);
            isNew = true;

            ruleName = "Création de l'ordinateur";
            inputValue = content.Hardware?.Uuid ?? agent.DeviceId;
        }

        ComputerSnapshot before = ComputerSnapshot.Capture(computer);

        ApplyHardware(computer, content);
        ApplyComponents(computer, content, settings);
        if (settings.ImportSoftwares) ApplySoftwares(computer, content);
        if (settings.ImportMonitors) ApplyMonitors(computer, content);
        ApplyVolumes(computer, content, settings);
        if (settings.ImportBatteries) ApplyBatteries(computer, content);
        ApplyNetworkPorts(computer, content);

        computer.LastInventoryAt = DateTime.UtcNow;
        computer.Status = ComputerStatus.InProduction;

        computer.ImportHistories.Add(new ComputerImportHistory
        {
            OccurredAt = DateTime.UtcNow,
            RuleName = ruleName,
            Module = "Inventaire",
            AgentIdentifier = agent.DeviceId ?? agent.Hostname ?? agent.AgentUuid,
            InputValue = inputValue
        });

        foreach (ComputerHistoryEntry entry in BuildHistoryEntries(before, computer, isNew))
        {
            computer.HistoryEntries.Add(entry);
        }

        agent.Computer = computer;

        await db.SaveChangesAsync(cancellationToken);
        return computer;
    }

    /// <summary>
    /// Utilisé par l'import manuel de fichier (onglet "Importer depuis un fichier" de
    /// /admin/inventory) : contrairement à AgentController.HandleInventoryAsync, il n'y
    /// a pas d'en-tête GLPI-Agent-ID côté fichier, donc le "deviceid" du contenu importé sert
    /// directement d'identité d'agent stable pour retrouver/créer l'ordinateur correspondant.
    /// </summary>
    public async Task<Computer> ImportFromDeviceIdAsync(string deviceId, InventoryContent content, CancellationToken cancellationToken = default)
    {
        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == deviceId, cancellationToken);
        if (agent is null)
        {
            agent = new GlpiAgent { AgentUuid = deviceId, DeviceId = deviceId };
            db.Agents.Add(agent);
        }

        return await ImportAsync(agent, content, cancellationToken);
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
    private static void ApplyComponents(Computer computer, InventoryContent content, InventorySettings settings)
    {
        computer.Components.Clear();

        if (settings.ImportCpus)
        {
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
        }

        if (settings.ImportMemories)
        {
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
        }

        if (settings.ImportDisks)
        {
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
        }

        if (settings.ImportNetworkCards)
        {
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
    private static void ApplyVolumes(Computer computer, InventoryContent content, InventorySettings settings)
    {
        computer.Volumes.Clear();

        foreach (InventoryDrive drive in content.Drives)
        {
            if (!ShouldImportDrive(drive, settings)) continue;

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
    /// L'onglet Configuration de l'inventaire distingue "Volumes"/"Lecteurs réseaux"/"Lecteurs
    /// amovibles" comme dans GLPI. L'agent Windows remonte "type" comme le code DriveType de
    /// Win32_LogicalDisk (2 = amovible, 4 = réseau) ; les autres valeurs (3 = disque local, ou
    /// absentes sur les agents non-Windows) sont traitées comme des volumes classiques.
    /// </summary>
    private static bool ShouldImportDrive(InventoryDrive drive, InventorySettings settings) => drive.Type switch
    {
        "2" => settings.ImportRemovableDrives,
        "4" => settings.ImportNetworkDrives,
        _ => settings.ImportVolumes
    };

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

    /// <summary>
    /// Photo de l'état d'un ordinateur juste avant application d'un inventaire, utilisée pour
    /// produire les entrées de l'onglet "Historique" en comparant avant/après. Les listes sont
    /// des clés textuelles (pas les entités elles-mêmes) car Apply* vide et recrée les collections
    /// à chaque import : comparer par identité EF ne distinguerait pas "inchangé" de "recréé".
    /// </summary>
    private sealed record ComputerSnapshot(
        string? Name, string? SerialNumber, string? Manufacturer, string? Model,
        string? OperatingSystem, string? OsVersion, string? OsKernelVersion,
        string? HardwareUuid, string? ChassisType, int? TotalMemoryMb,
        string? LastLoggedUser, string? VmSystem, ComputerStatus Status, DateTime? LastInventoryAt,
        HashSet<string> Components, HashSet<string> Softwares, HashSet<string> Monitors,
        HashSet<string> Volumes, HashSet<string> Batteries, HashSet<string> NetworkPorts)
    {
        public static ComputerSnapshot Capture(Computer c) => new(
            c.Name, c.SerialNumber, c.Manufacturer, c.Model,
            c.OperatingSystem, c.OsVersion, c.OsKernelVersion,
            c.HardwareUuid, c.ChassisType, c.TotalMemoryMb,
            c.LastLoggedUser, c.VmSystem, c.Status, c.LastInventoryAt,
            c.Components.Select(ComponentKey).ToHashSet(),
            c.Softwares.Select(SoftwareKey).ToHashSet(),
            c.Peripherals.Where(p => p.Kind == PeripheralKind.Monitor).Select(PeripheralKey).ToHashSet(),
            c.Volumes.Select(VolumeKey).ToHashSet(),
            c.Batteries.Select(BatteryKey).ToHashSet(),
            c.NetworkPorts.Select(NetworkPortKey).ToHashSet());
    }

    private static string ComponentKey(ComputerComponent c) =>
        $"{ComponentTypeLabel(c.Type)} {c.Designation}" + (c.Capacity is { Length: > 0 } cap ? $" ({cap})" : "");

    private static string SoftwareKey(ComputerSoftware s) => s.Version is { Length: > 0 } v ? $"{s.Name} ({v})" : s.Name;

    private static string PeripheralKey(ComputerPeripheral p) => p.Designation;

    private static string VolumeKey(ComputerVolume v) => v.MountPoint is { Length: > 0 } mp ? $"{v.Name} ({mp})" : v.Name;

    private static string BatteryKey(ComputerBattery b) => b.Serial is { Length: > 0 } s ? $"{b.Name} ({s})" : b.Name;

    private static string NetworkPortKey(ComputerNetworkPort p) => p.MacAddress is { Length: > 0 } mac ? $"{p.Designation} ({mac})" : p.Designation;

    private static string ComponentTypeLabel(ComponentType type) => type switch
    {
        ComponentType.Cpu => "Processeur",
        ComponentType.Ram => "Barrette mémoire",
        ComponentType.Disk => "Disque dur",
        ComponentType.NetworkCard => "Carte réseau",
        ComponentType.Gpu => "Carte graphique",
        ComponentType.Motherboard => "Carte mère",
        _ => "Composant"
    };

    /// <summary>
    /// Construit les entrées d'historique en comparant <paramref name="before"/> (état capturé
    /// avant l'import) à l'état courant de <paramref name="computer"/> (après Apply*). Sur
    /// création (<paramref name="isNew"/>), on ne journalise que les ajouts de sous-éléments —
    /// pas les champs scalaires "null → valeur", qui ne correspondent à aucun changement observable.
    /// </summary>
    private static IEnumerable<ComputerHistoryEntry> BuildHistoryEntries(ComputerSnapshot before, Computer computer, bool isNew)
    {
        if (!isNew)
        {
            if (before.Name != computer.Name) yield return FieldChange("Nom", before.Name, computer.Name);
            if (before.SerialNumber != computer.SerialNumber) yield return FieldChange("Numéro de série", before.SerialNumber, computer.SerialNumber);
            if (before.Manufacturer != computer.Manufacturer) yield return FieldChange("Fabricant", before.Manufacturer, computer.Manufacturer);
            if (before.Model != computer.Model) yield return FieldChange("Modèle", before.Model, computer.Model);
            if (before.OperatingSystem != computer.OperatingSystem) yield return FieldChange("Système d'exploitation", before.OperatingSystem, computer.OperatingSystem);
            if (before.OsVersion != computer.OsVersion) yield return FieldChange("Version de l'OS", before.OsVersion, computer.OsVersion);
            if (before.OsKernelVersion != computer.OsKernelVersion) yield return FieldChange("Version du noyau", before.OsKernelVersion, computer.OsKernelVersion);
            if (before.HardwareUuid != computer.HardwareUuid) yield return FieldChange("UUID matériel", before.HardwareUuid, computer.HardwareUuid);
            if (before.ChassisType != computer.ChassisType) yield return FieldChange("Type de châssis", before.ChassisType, computer.ChassisType);
            if (before.TotalMemoryMb != computer.TotalMemoryMb) yield return FieldChange("Mémoire totale", FormatMemory(before.TotalMemoryMb), FormatMemory(computer.TotalMemoryMb));
            if (before.LastLoggedUser != computer.LastLoggedUser) yield return FieldChange("Dernier utilisateur connecté", before.LastLoggedUser, computer.LastLoggedUser);
            if (before.VmSystem != computer.VmSystem) yield return FieldChange("Virtualisation", before.VmSystem, computer.VmSystem);
            if (before.LastInventoryAt != computer.LastInventoryAt) yield return FieldChange("Date de dernier inventaire", FormatDate(before.LastInventoryAt), FormatDate(computer.LastInventoryAt));
            if (before.Status != computer.Status) yield return FieldChange("Statut", StatusLabel(before.Status), StatusLabel(computer.Status));
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Components, computer.Components.Select(ComponentKey).ToHashSet(),
                     "Composants", "Ajouter un composant", "Supprimer un composant"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Softwares, computer.Softwares.Select(SoftwareKey).ToHashSet(),
                     "Logiciels", "Ajout d'un lien avec un élément", "Suppression du lien avec l'élément"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Monitors,
                     computer.Peripherals.Where(p => p.Kind == PeripheralKind.Monitor).Select(PeripheralKey).ToHashSet(),
                     "Périphériques", "Ajout d'un lien avec un élément", "Suppression du lien avec l'élément"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Volumes, computer.Volumes.Select(VolumeKey).ToHashSet(),
                     "Volumes", "Ajouter un volume", "Supprimer un volume"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Batteries, computer.Batteries.Select(BatteryKey).ToHashSet(),
                     "Batteries", "Ajouter un composant", "Supprimer un composant"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.NetworkPorts, computer.NetworkPorts.Select(NetworkPortKey).ToHashSet(),
                     "Ports réseau", "Ajouter un port réseau", "Supprimer un port réseau"))
        {
            yield return entry;
        }
    }

    private static IEnumerable<ComputerHistoryEntry> DiffKeyedSet(HashSet<string> before, HashSet<string> after, string field, string addVerb, string removeVerb)
    {
        foreach (string added in after.Except(before))
        {
            yield return new ComputerHistoryEntry { User = HistoryUser, Field = field, Description = $"{addVerb} : {added}" };
        }

        foreach (string removed in before.Except(after))
        {
            yield return new ComputerHistoryEntry { User = HistoryUser, Field = field, Description = $"{removeVerb} : {removed}" };
        }
    }

    private static ComputerHistoryEntry FieldChange(string field, string? oldValue, string? newValue) => new()
    {
        User = HistoryUser,
        Field = field,
        Description = $"Changement de {oldValue ?? "—"} à {newValue ?? "—"}"
    };

    private static string? FormatMemory(int? memoryMb) => memoryMb is { } mb ? $"{mb} Mo" : null;

    private static string? FormatDate(DateTime? date) => date?.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    private static string StatusLabel(ComputerStatus status) => status switch
    {
        ComputerStatus.InStock => "En stock",
        ComputerStatus.InProduction => "En production",
        ComputerStatus.Broken => "En panne",
        ComputerStatus.Retired => "Réformé",
        _ => status.ToString()
    };
}
