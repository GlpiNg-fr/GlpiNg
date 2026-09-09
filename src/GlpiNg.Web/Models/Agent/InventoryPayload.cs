using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlpiNg.Web.Models.Agent;

/// <summary>
/// Accepte indifféremment un string, un bool ou un nombre JSON et les convertit en string.
/// Nécessaire car l'agent GLPI envoie certains champs (virtualdev, etc.) tantôt comme
/// chaîne ("1"), tantôt comme booléen (true) ou nombre selon la version de l'agent.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.True => "1",
            JsonTokenType.False => "0",
            JsonTokenType.Number when reader.TryGetInt64(out long l) => l.ToString(),
            JsonTokenType.Number when reader.TryGetDouble(out double d) => d.ToString(),
            JsonTokenType.Null => null,
            _ => JsonDocument.ParseValue(ref reader).RootElement.ToString()
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

/// <summary>
/// Accepte indifféremment un bool, un string ("1"/"0"/"true"/"false") ou un nombre JSON pour
/// les champs booléens de content.antivirus (enabled, uptodate), remontés de façon incohérente
/// selon les versions/plateformes d'agent — même problème que <see cref="FlexibleStringConverter"/>.
/// </summary>
public class FlexibleBoolConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => reader.GetString()?.Trim().ToLowerInvariant() switch
            {
                "1" or "true" or "yes" => true,
                "0" or "false" or "no" or "" => false,
                _ => null
            },
            JsonTokenType.Number when reader.TryGetInt64(out long l) => l != 0,
            JsonTokenType.Null => null,
            _ => null
        };

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteBooleanValue(value.Value);
    }
}

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
/// Sections volontairement non reprises, faute d'équivalent dans le modèle GlpiNg : machines
/// virtuelles, processus, variables d'environnement, règles de pare-feu, comptes et groupes
/// locaux, licences logicielles, journal de connexions. Elles sont ignorées silencieusement par le
/// (dé)sérialiseur, comme toute section non mappée.
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

    [JsonPropertyName("usbdevices")]
    public List<InventoryUsbDevice> UsbDevices { get; set; } = [];

    [JsonPropertyName("inputs")]
    public List<InventoryInput> Inputs { get; set; } = [];

    [JsonPropertyName("antivirus")]
    public List<InventoryAntivirus> Antivirus { get; set; } = [];

    [JsonPropertyName("controllers")]
    public List<InventoryGenericDevice> Controllers { get; set; } = [];

    [JsonPropertyName("videos")]
    public List<InventoryGenericDevice> Videos { get; set; } = [];

    [JsonPropertyName("sounds")]
    public List<InventoryGenericDevice> Sounds { get; set; } = [];

    [JsonPropertyName("modems")]
    public List<InventoryGenericDevice> Modems { get; set; } = [];

    /// <summary>
    /// Connecteurs physiques de la machine (section "ports" du protocole) : USB, série, parallèle,
    /// vidéo, audio... Remontés par l'agent depuis Win32_PortConnector ou le type 8 de dmidecode.
    /// </summary>
    [JsonPropertyName("ports")]
    public List<InventoryPort> Ports { get; set; } = [];

    [JsonPropertyName("simcards")]
    public List<InventorySimCard> SimCards { get; set; } = [];

    /// <summary>Outil de prise en main à distance déclaré par l'agent (TeamViewer, AnyDesk, ...).</summary>
    [JsonPropertyName("remote_mgmt")]
    public List<InventoryRemoteManagement> RemoteManagement { get; set; } = [];

    /// <summary>Couples clé/valeur libres de l'agent. Porte le TAG, d'où l'entité de rattachement est déduite.</summary>
    [JsonPropertyName("accountinfo")]
    public List<InventoryAccountInfo> AccountInfo { get; set; } = [];

    [JsonPropertyName("versionclient")]
    public string? VersionClient { get; set; }

    /// <summary>
    /// TAG déclaré par l'agent dans le corps de l'inventaire. GLPI-Agent l'expose comme une entrée
    /// « accountinfo » de clé TAG plutôt que comme un champ dédié.
    /// </summary>
    [JsonIgnore]
    public string? Tag => AccountInfo
        .FirstOrDefault(entry => string.Equals(entry.KeyName, "TAG", StringComparison.OrdinalIgnoreCase))?
        .KeyValue;
}

/// <summary>Couple clé/valeur de la section "accountinfo".</summary>
public class InventoryAccountInfo
{
    [JsonPropertyName("keyname")]
    public string? KeyName { get; set; }

    [JsonPropertyName("keyvalue")]
    public string? KeyValue { get; set; }
}

/// <summary>
/// Forme commune aux sections "controllers", "videos", "sounds" et "modems" : GLPI-Agent y remonte
/// les mêmes champs, à quelques colonnes spécifiques près (mémoire d'une carte graphique,
/// résolution) qui ne sont pas reprises ici — le modèle ComputerComponent n'a que désignation,
/// capacité et numéro de série.
/// </summary>
public class InventoryGenericDevice
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Mémoire vidéo en Mo, renseignée seulement par la section "videos".</summary>
    [JsonPropertyName("memory")]
    public int? Memory { get; set; }

    /// <summary>Libellé retenu pour le composant : le premier champ non vide, du plus précis au plus vague.</summary>
    [JsonIgnore]
    public string? Designation =>
        new[] { Name, Caption, Description, Manufacturer }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

/// <summary>Carte SIM remontée par l'agent (section "simcards").</summary>
public class InventorySimCard
{
    /// <summary>Identifiant gravé sur la carte : c'est la clé de corrélation avec un actif Carte SIM existant.</summary>
    [JsonPropertyName("iccid")]
    public string? Iccid { get; set; }

    [JsonPropertyName("imsi")]
    public string? Imsi { get; set; }

    [JsonPropertyName("operator_name")]
    public string? OperatorName { get; set; }

    [JsonPropertyName("phone_number")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

/// <summary>Outil de prise en main à distance (section "remote_mgmt").</summary>
public class InventoryRemoteManagement
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
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

    /// <summary>Domaine Active Directory ou groupe de travail Windows du poste.</summary>
    [JsonPropertyName("workgroup")]
    public string? Workgroup { get; set; }
}

public class InventoryBios
{
    [JsonPropertyName("smodel")]
    public string? SystemModel { get; set; }

    [JsonPropertyName("smanufacturer")]
    public string? SystemManufacturer { get; set; }

    [JsonPropertyName("ssn")]
    public string? SystemSerial { get; set; }

    /// <summary>Version du BIOS/UEFI lui-même — à distinguer des champs "s*" ci-dessus, qui
    /// décrivent la machine et non le firmware.</summary>
    [JsonPropertyName("bversion")]
    public string? BiosVersion { get; set; }

    [JsonPropertyName("bmanufacturer")]
    public string? BiosManufacturer { get; set; }

    /// <summary>Date du BIOS, telle que remontée par l'agent (format libre selon la plateforme).</summary>
    [JsonPropertyName("bdate")]
    public string? BiosDate { get; set; }
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

    [JsonPropertyName("mac")]
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
    [JsonConverter(typeof(FlexibleStringConverter))]
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
/// <summary>Un connecteur physique remonté par la section "ports".</summary>
public class InventoryPort
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Type de connecteur tel que nommé par l'agent : "USB", "Serial", "Video", ...</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

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

    /// <summary>Capacité réelle actuelle (diminue avec l'usure de la batterie), en mWh.</summary>
    [JsonPropertyName("real_capacity")]
    public int? RealCapacityMwh { get; set; }

    [JsonPropertyName("date")]
    public string? ManufactureDate { get; set; }
}

/// <summary>content.usbdevices — périphériques USB connectés (souris, clavier, webcam, hub USB, etc.).</summary>
public class InventoryUsbDevice
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    [JsonPropertyName("vendorid")]
    public string? VendorId { get; set; }

    [JsonPropertyName("productid")]
    public string? ProductId { get; set; }

    [JsonPropertyName("class")]
    public string? Class { get; set; }

    [JsonPropertyName("subclass")]
    public string? SubClass { get; set; }
}

/// <summary>content.inputs — périphériques d'entrée (clavier, souris, touchpad, etc.).</summary>
public class InventoryInput
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("interface")]
    public string? Interface { get; set; }

    [JsonPropertyName("layout")]
    public string? Layout { get; set; }

    [JsonPropertyName("pointingtype")]
    public string? PointingType { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

/// <summary>content.antivirus — logiciels antivirus détectés par l'agent.</summary>
public class InventoryAntivirus
{
    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("guid")]
    public string? Guid { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("enabled")]
    [JsonConverter(typeof(FlexibleBoolConverter))]
    public bool? Enabled { get; set; }

    [JsonPropertyName("uptodate")]
    [JsonConverter(typeof(FlexibleBoolConverter))]
    public bool? UpToDate { get; set; }

    /// <summary>Date d'expiration de la licence (format libre, tel que remonté par l'agent).</summary>
    [JsonPropertyName("expiration")]
    public string? Expiration { get; set; }

    /// <summary>Date de création de la base de signatures (format libre).</summary>
    [JsonPropertyName("base_creation")]
    public string? BaseCreationDate { get; set; }

    [JsonPropertyName("base_version")]
    public string? BaseVersion { get; set; }
}
