using System.Text.Json.Serialization;

namespace GlpiNg.Web.Models.Agent;

/// <summary>
/// Requête "inventory" du protocole JSON GLPI-Agent.
/// Référence: https://glpi-json-protocol.readthedocs.io/en/latest/inventory.html
/// et https://github.com/glpi-project/inventory_format (inventory.schema.json)
/// </summary>
public class InventoryRequest
{
    /// <summary>"inventory", "netdiscovery" ou "netinventory" — défaut "inventory".</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("deviceid")]
    public required string DeviceId { get; set; }

    [JsonPropertyName("content")]
    public required InventoryContent Content { get; set; }

    /// <summary>Défaut "Computer" — support de la généricité GLPI 11+ non géré ici.</summary>
    [JsonPropertyName("itemtype")]
    public string? ItemType { get; set; }
}

/// <summary>
/// Sous-ensemble du contenu d'inventaire réellement exploité par GlpiNg pour l'instant.
/// Le schéma officiel couvre bien plus de sections (antivirus, printers, virtualmachines...)
/// — celles-ci sont ignorées silencieusement par le (dé)sérialiseur tant qu'elles ne sont pas mappées ici.
/// </summary>
public class InventoryContent
{
    [JsonPropertyName("hardware")]
    public InventoryHardware? Hardware { get; set; }

    [JsonPropertyName("bios")]
    public InventoryBios? Bios { get; set; }

    [JsonPropertyName("operatingsystem")]
    public InventoryOperatingSystem? OperatingSystem { get; set; }

    [JsonPropertyName("cpus")]
    public List<InventoryCpu> Cpus { get; set; } = [];

    [JsonPropertyName("memories")]
    public List<InventoryMemory> Memories { get; set; } = [];

    [JsonPropertyName("storages")]
    public List<InventoryStorage> Storages { get; set; } = [];

    [JsonPropertyName("drives")]
    public List<InventoryDrive> Drives { get; set; } = [];

    [JsonPropertyName("networks")]
    public List<InventoryNetwork> Networks { get; set; } = [];

    [JsonPropertyName("softwares")]
    public List<InventorySoftware> Softwares { get; set; } = [];

    [JsonPropertyName("monitors")]
    public List<InventoryMonitor> Monitors { get; set; } = [];

    [JsonPropertyName("batteries")]
    public List<InventoryBattery> Batteries { get; set; } = [];

    [JsonPropertyName("versionclient")]
    public string? VersionClient { get; set; }
}

public class InventoryHardware
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    [JsonPropertyName("memory")]
    public int? MemoryMb { get; set; }

    [JsonPropertyName("chassis_type")]
    public string? ChassisType { get; set; }

    [JsonPropertyName("lastloggeduser")]
    public string? LastLoggedUser { get; set; }

    [JsonPropertyName("vmsystem")]
    public string? VmSystem { get; set; }
}

public class InventoryBios
{
    [JsonPropertyName("smodel")]
    public string? SystemModel { get; set; }

    [JsonPropertyName("smanufacturer")]
    public string? SystemManufacturer { get; set; }

    [JsonPropertyName("ssn")]
    public string? SystemSerial { get; set; }
}

public class InventoryOperatingSystem
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("kernel_version")]
    public string? KernelVersion { get; set; }

    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }
}

public class InventoryCpu
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    /// <summary>Fréquence en MHz.</summary>
    [JsonPropertyName("speed")]
    public int? SpeedMhz { get; set; }

    [JsonPropertyName("core")]
    public int? Cores { get; set; }
}

public class InventoryMemory
{
    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("serialnumber")]
    public string? Serial { get; set; }

    /// <summary>Capacité en Mo — confirmé numérique (ex: 8192) sur un payload réel.</summary>
    [JsonPropertyName("capacity")]
    public int? CapacityMb { get; set; }
}

public class InventoryStorage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("serialnumber")]
    public string? Serial { get; set; }

    /// <summary>Capacité en Mo.</summary>
    [JsonPropertyName("disksize")]
    public long? DiskSizeMb { get; set; }
}

/// <summary>content.drives — volumes/partitions de disque, distincts des disques physiques (content.storages).</summary>
public class InventoryDrive
{
    [JsonPropertyName("volumn")]
    public string? Volume { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("letter")]
    public string? Letter { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("filesystem")]
    public string? FileSystem { get; set; }

    /// <summary>Taille totale en Mo.</summary>
    [JsonPropertyName("total")]
    public long? TotalMb { get; set; }

    /// <summary>Espace libre en Mo.</summary>
    [JsonPropertyName("free")]
    public long? FreeMb { get; set; }
}

public class InventoryNetwork
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("macaddr")]
    public string? MacAddress { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("ipaddress")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("ipmask")]
    public string? IpMask { get; set; }

    [JsonPropertyName("ipgateway")]
    public string? IpGateway { get; set; }

    [JsonPropertyName("ipsubnet")]
    public string? IpSubnet { get; set; }

    [JsonPropertyName("ipdhcp")]
    public string? IpDhcp { get; set; }

    [JsonPropertyName("mtu")]
    public int? Mtu { get; set; }

    /// <summary>Vitesse de liaison en Mb/s.</summary>
    [JsonPropertyName("speed")]
    public int? SpeedMbps { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>"1" pour une interface virtuelle (tap, bridge, vlan...), remonté comme chaîne par l'agent.</summary>
    [JsonPropertyName("virtualdev")]
    public string? VirtualDevice { get; set; }
}

public class InventorySoftware
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("install_date")]
    public string? InstallDate { get; set; }
}

public class InventoryMonitor
{
    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("serial")]
    public string? Serial { get; set; }
}

/// <summary>content.batteries.</summary>
public class InventoryBattery
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    [JsonPropertyName("chemistry")]
    public string? Chemistry { get; set; }

    /// <summary>Tension en mV.</summary>
    [JsonPropertyName("voltage")]
    public int? VoltageMv { get; set; }

    /// <summary>Capacité constructeur en mWh.</summary>
    [JsonPropertyName("capacity")]
    public int? CapacityMwh { get; set; }

    [JsonPropertyName("date")]
    public string? ManufactureDate { get; set; }
}
