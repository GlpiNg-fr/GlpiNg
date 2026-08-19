using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Modules.Inventory.Services;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Agent;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Importe le contenu d'une requête "inventory" du protocole GLPI-Agent
/// dans les entités Computer / ComputerComponent, en le rattachant à l'agent fourni.
/// </summary>
public class InventoryImportService(
    GlpiNgDbContext db,
    SettingsCacheService settingsStore,
    NotificationDispatchService notificationDispatch,
    IHttpContextAccessor httpContextAccessor)
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
            .Include(c => c.Antiviruses)
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
                     .Include(c => c.Antiviruses)
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

        Dictionary<DictionaryRuleType, List<DictionaryRule>> dictionaries = await LoadActiveDictionaryRulesAsync(cancellationToken);

        ApplyHardware(computer, content, dictionaries);
        ApplyComponents(computer, content, settings);
        if (settings.ImportSoftwares) ApplySoftwares(computer, content, dictionaries);
        if (settings.ImportMonitors) ApplyMonitors(computer, content);
        if (settings.ImportPeripherals) await ApplyPeripheralsAsync(computer, content, cancellationToken);
        ApplyVolumes(computer, content, settings);
        if (settings.ImportBatteries) ApplyBatteries(computer, content);
        ApplyNetworkPorts(computer, content);
        if (settings.ImportAntivirus) ApplyAntivirus(computer, content);

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

        if (isNew)
        {
            await PublishNewComputerNotificationAsync(computer, cancellationToken);
        }

        return computer;
    }

    /// <summary>Déclenche l'événement "Nouvel ordinateur découvert" (voir NotificationEventCatalog.InventoryComputer) après la création effective en base.</summary>
    private async Task PublishNewComputerNotificationAsync(Computer computer, CancellationToken cancellationToken)
    {
        string? baseUrl = BuildBaseUrl();

        Dictionary<string, string?> variables = new()
        {
            ["computer.name"] = computer.Name,
            ["computer.serial"] = computer.SerialNumber,
            ["computer.manufacturer"] = computer.Manufacturer,
            ["computer.model"] = computer.Model,
            ["computer.os"] = computer.OperatingSystem,
            ["computer.url"] = baseUrl is null ? null : $"{baseUrl}/parc/computer/{computer.Id}",
        };

        await notificationDispatch.PublishAsync(
            NotificationEventCatalog.InventoryComputer, NotificationEventCatalog.EventNew, computer.Id, variables, cancellationToken);
    }

    /// <summary>
    /// Reconstruit l'origine (schéma+hôte) de la requête HTTP courante pour les balises ##...url##
    /// des notifications. Null hors contexte de requête (ex. import depuis un fichier via une tâche
    /// arrière-plan) : le lien est alors simplement omis plutôt que de pointer vers une URL fausse.
    /// </summary>
    private string? BuildBaseUrl()
    {
        HttpRequest? request = httpContextAccessor.HttpContext?.Request;
        return request is null ? null : $"{request.Scheme}://{request.Host}";
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

    private static void ApplyHardware(Computer computer, InventoryContent content, IReadOnlyDictionary<DictionaryRuleType, List<DictionaryRule>> dictionaries)
    {
        if (content.Hardware is { } hardware)
        {
            computer.Name = string.IsNullOrWhiteSpace(hardware.Name) ? computer.Name : hardware.Name;
            computer.HardwareUuid = hardware.Uuid ?? computer.HardwareUuid;
            computer.ChassisType = hardware.ChassisType ?? computer.ChassisType;
            computer.TotalMemoryMb = hardware.MemoryMb ?? computer.TotalMemoryMb;
            computer.LastLoggedUser = hardware.LastLoggedUser ?? computer.LastLoggedUser;
            computer.VmSystem = hardware.VmSystem ?? computer.VmSystem;
            computer.Domain = hardware.Workgroup ?? computer.Domain;
        }

        if (content.Bios is { } bios)
        {
            DictionaryRuleEngine.Result manufacturer = DictionaryRuleEngine.Apply(bios.SystemManufacturer, GetRules(dictionaries, DictionaryRuleType.Manufacturer));
            if (!manufacturer.Ignore) computer.Manufacturer = manufacturer.Value ?? computer.Manufacturer;

            DictionaryRuleEngine.Result model = DictionaryRuleEngine.Apply(bios.SystemModel, GetRules(dictionaries, DictionaryRuleType.ComputerModel));
            if (!model.Ignore) computer.Model = model.Value ?? computer.Model;

            computer.SerialNumber = bios.SystemSerial ?? computer.SerialNumber;
        }

        if (content.OperatingSystem is { } os)
        {
            DictionaryRuleEngine.Result osName = DictionaryRuleEngine.Apply(os.FullName ?? os.Name, GetRules(dictionaries, DictionaryRuleType.OperatingSystem));
            if (!osName.Ignore) computer.OperatingSystem = osName.Value ?? computer.OperatingSystem;

            DictionaryRuleEngine.Result osVersion = DictionaryRuleEngine.Apply(os.Version, GetRules(dictionaries, DictionaryRuleType.OperatingSystemVersion));
            if (!osVersion.Ignore) computer.OsVersion = osVersion.Value ?? computer.OsVersion;

            computer.OsKernelVersion = os.KernelVersion ?? computer.OsKernelVersion;
        }
    }

    private static readonly List<DictionaryRule> EmptyDictionaryRules = [];

    private static List<DictionaryRule> GetRules(IReadOnlyDictionary<DictionaryRuleType, List<DictionaryRule>> dictionaries, DictionaryRuleType type) =>
        dictionaries.TryGetValue(type, out List<DictionaryRule>? rules) ? rules : EmptyDictionaryRules;

    private async Task<Dictionary<DictionaryRuleType, List<DictionaryRule>>> LoadActiveDictionaryRulesAsync(CancellationToken cancellationToken)
    {
        List<DictionaryRule> rules = await db.Set<DictionaryRule>()
            .AsNoTracking()
            .Include(r => r.Criteria)
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);

        return rules.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.ToList());
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
    private static void ApplySoftwares(Computer computer, InventoryContent content, IReadOnlyDictionary<DictionaryRuleType, List<DictionaryRule>> dictionaries)
    {
        computer.Softwares.Clear();

        List<DictionaryRule> rules = GetRules(dictionaries, DictionaryRuleType.Software);

        foreach (InventorySoftware software in content.Softwares)
        {
            if (string.IsNullOrWhiteSpace(software.Name)) continue;

            DictionaryRuleEngine.Result result = DictionaryRuleEngine.Apply(software.Name, software.Publisher, rules);
            if (result.Ignore) continue;

            computer.Softwares.Add(new ComputerSoftware
            {
                Name = result.Value ?? software.Name,
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
    /// Crée ou met à jour les entités <see cref="Peripheral"/> autonomes à partir des
    /// périphériques USB et d'entrée remontés par l'agent, et les rattache à l'ordinateur.
    /// La corrélation se fait par numéro de série (quand disponible), sinon par
    /// nom+fabricant sur le même ordinateur, pour éviter les doublons à chaque inventaire.
    /// </summary>
    private async Task ApplyPeripheralsAsync(Computer computer, InventoryContent content, CancellationToken cancellationToken)
    {
        List<Peripheral> existingPeripherals = await db.Set<Peripheral>()
            .Where(p => p.ComputerId == computer.Id)
            .ToListAsync(cancellationToken);

        HashSet<int> seenIds = [];

        foreach (InventoryUsbDevice usb in content.UsbDevices)
        {
            string name = usb.Name ?? "Périphérique USB inconnu";
            if (string.IsNullOrWhiteSpace(name) || name == "usb") continue;

            string type = ResolveUsbType(usb);

            Peripheral? peripheral = FindExistingPeripheral(existingPeripherals, name, usb.Manufacturer, usb.Serial);

            if (peripheral is null)
            {
                peripheral = new Peripheral { Name = name };
                db.Set<Peripheral>().Add(peripheral);
                existingPeripherals.Add(peripheral);
            }

            peripheral.Manufacturer = usb.Manufacturer ?? peripheral.Manufacturer;
            peripheral.SerialNumber = usb.Serial ?? peripheral.SerialNumber;
            peripheral.Type = type;
            peripheral.ComputerId = computer.Id;
            peripheral.Status = ComputerStatus.InProduction;
            peripheral.UpdatedAt = DateTime.UtcNow;

            if (peripheral.Id > 0) seenIds.Add(peripheral.Id);
        }

        foreach (InventoryInput input in content.Inputs)
        {
            string name = input.Name ?? input.Caption ?? input.Description ?? "Périphérique d'entrée inconnu";
            if (string.IsNullOrWhiteSpace(name)) continue;

            string type = ResolveInputType(input);

            Peripheral? peripheral = FindExistingPeripheral(existingPeripherals, name, input.Manufacturer, serial: null);

            if (peripheral is null)
            {
                peripheral = new Peripheral { Name = name };
                db.Set<Peripheral>().Add(peripheral);
                existingPeripherals.Add(peripheral);
            }

            peripheral.Manufacturer = input.Manufacturer ?? peripheral.Manufacturer;
            peripheral.Type = type;
            peripheral.ComputerId = computer.Id;
            peripheral.Status = ComputerStatus.InProduction;
            peripheral.UpdatedAt = DateTime.UtcNow;

            if (peripheral.Id > 0) seenIds.Add(peripheral.Id);
        }
    }

    private static Peripheral? FindExistingPeripheral(List<Peripheral> existingPeripherals, string name, string? manufacturer, string? serial)
    {
        if (!string.IsNullOrWhiteSpace(serial))
        {
            Peripheral? bySerial = existingPeripherals.FirstOrDefault(p =>
                string.Equals(p.SerialNumber, serial, StringComparison.OrdinalIgnoreCase));
            if (bySerial is not null) return bySerial;
        }

        return existingPeripherals.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Manufacturer, manufacturer, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveUsbType(InventoryUsbDevice usb)
    {
        if (usb.Class is "03" or "3") return "HID";
        if (usb.Class is "08" or "8") return "Stockage USB";
        if (usb.Class is "09" or "9") return "Hub USB";
        if (usb.Class is "0e" or "14" or "0E") return "Webcam";
        if (usb.Class is "01" or "1") return "Audio USB";
        if (usb.Class is "07" or "7") return "Imprimante USB";
        return "Périphérique USB";
    }

    private static string ResolveInputType(InventoryInput input)
    {
        if (input.PointingType is not null) return "Dispositif de pointage";
        if (input.Type?.Contains("keyboard", StringComparison.OrdinalIgnoreCase) == true
            || input.Layout is not null) return "Clavier";
        if (input.Type?.Contains("mouse", StringComparison.OrdinalIgnoreCase) == true
            || input.Type?.Contains("touchpad", StringComparison.OrdinalIgnoreCase) == true) return "Dispositif de pointage";
        return "Périphérique d'entrée";
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
                RealCapacityMwh = battery.RealCapacityMwh,
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
    /// Remplace intégralement les antivirus du poste par ceux de l'inventaire courant, même
    /// logique d'instantané complet que <see cref="ApplyComponents"/>.
    /// </summary>
    private static void ApplyAntivirus(Computer computer, InventoryContent content)
    {
        computer.Antiviruses.Clear();

        foreach (InventoryAntivirus antivirus in content.Antivirus)
        {
            if (string.IsNullOrWhiteSpace(antivirus.Name)) continue;

            computer.Antiviruses.Add(new ComputerAntivirus
            {
                Name = antivirus.Name,
                Company = antivirus.Company,
                Guid = antivirus.Guid,
                Version = antivirus.Version,
                Enabled = antivirus.Enabled,
                UpToDate = antivirus.UpToDate,
                Expiration = antivirus.Expiration,
                BaseCreationDate = antivirus.BaseCreationDate,
                BaseVersion = antivirus.BaseVersion
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
        string? LastLoggedUser, string? VmSystem, string? Domain, ComputerStatus Status, DateTime? LastInventoryAt,
        HashSet<string> Components, HashSet<string> Softwares, HashSet<string> Monitors,
        HashSet<string> Volumes, HashSet<string> Batteries, HashSet<string> NetworkPorts,
        HashSet<string> Antiviruses)
    {
        public static ComputerSnapshot Capture(Computer c) => new(
            c.Name, c.SerialNumber, c.Manufacturer, c.Model,
            c.OperatingSystem, c.OsVersion, c.OsKernelVersion,
            c.HardwareUuid, c.ChassisType, c.TotalMemoryMb,
            c.LastLoggedUser, c.VmSystem, c.Domain, c.Status, c.LastInventoryAt,
            c.Components.Select(ComponentKey).ToHashSet(),
            c.Softwares.Select(SoftwareKey).ToHashSet(),
            c.Peripherals.Where(p => p.Kind == PeripheralKind.Monitor).Select(PeripheralKey).ToHashSet(),
            c.Volumes.Select(VolumeKey).ToHashSet(),
            c.Batteries.Select(BatteryKey).ToHashSet(),
            c.NetworkPorts.Select(NetworkPortKey).ToHashSet(),
            c.Antiviruses.Select(AntivirusKey).ToHashSet());
    }

    private static string ComponentKey(ComputerComponent c) =>
        $"{ComponentTypeLabel(c.Type)} {c.Designation}" + (c.Capacity is { Length: > 0 } cap ? $" ({cap})" : "");

    private static string SoftwareKey(ComputerSoftware s) => s.Version is { Length: > 0 } v ? $"{s.Name} ({v})" : s.Name;

    private static string PeripheralKey(ComputerPeripheral p) => p.Designation;

    private static string VolumeKey(ComputerVolume v) => v.MountPoint is { Length: > 0 } mp ? $"{v.Name} ({mp})" : v.Name;

    private static string BatteryKey(ComputerBattery b) => b.Serial is { Length: > 0 } s ? $"{b.Name} ({s})" : b.Name;

    private static string NetworkPortKey(ComputerNetworkPort p) => p.MacAddress is { Length: > 0 } mac ? $"{p.Designation} ({mac})" : p.Designation;

    private static string AntivirusKey(ComputerAntivirus a) => a.Version is { Length: > 0 } v ? $"{a.Name} ({v})" : a.Name;

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
            if (before.Domain != computer.Domain) yield return FieldChange("Domaine", before.Domain, computer.Domain);
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

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Antiviruses, computer.Antiviruses.Select(AntivirusKey).ToHashSet(),
                     "Antivirus", "Ajouter un antivirus", "Supprimer un antivirus"))
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
