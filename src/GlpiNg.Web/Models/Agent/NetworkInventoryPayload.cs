using System.Text.Json.Serialization;

namespace GlpiNg.Web.Models.Agent;

/// <summary>
/// Corps attendu pour les actions "netdiscovery"/"netinventory" — résultats d'un
/// <see cref="Modules.Deployment.Models.NetworkTaskJob"/> remontés par l'agent après exécution
/// d'un job récupéré via "getNetDiscoveryJobs"/"getNetInventoryJobs" (voir
/// AgentController.HandleGetNetworkJobsCoreAsync). Forme auto-inventée (voir le commentaire de
/// classe d'AgentController) : ce n'est PAS le schéma "inventory" standard
/// (<see cref="InventoryRequest"/>), qui suppose une section "hardware" et créerait un Computer
/// "Inconnu" si on le réutilisait tel quel sur un payload de scan réseau sans matériel.
/// </summary>
public class NetworkInventoryRequest
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("deviceid")]
    public required string DeviceId { get; set; }

    [JsonPropertyName("content")]
    public required NetworkInventoryContent Content { get; set; }
}

public class NetworkInventoryContent
{
    [JsonPropertyName("devices")]
    public List<NetworkInventoryDevice> Devices { get; set; } = [];
}

/// <summary>Un équipement réseau trouvé pendant le scan — <see cref="SysDescr"/>/<see cref="SysName"/>/
/// <see cref="SysContact"/>/<see cref="SysLocation"/> ne sont renseignés que pour un résultat
/// "netinventory" (interrogation SNMP), une simple "netdiscovery" (ping/ARP) ne les connaît pas.</summary>
public class NetworkInventoryDevice
{
    [JsonPropertyName("ip")]
    public required string Ip { get; set; }

    [JsonPropertyName("mac")]
    public string? Mac { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("sysdescr")]
    public string? SysDescr { get; set; }

    [JsonPropertyName("sysname")]
    public string? SysName { get; set; }

    [JsonPropertyName("syscontact")]
    public string? SysContact { get; set; }

    [JsonPropertyName("syslocation")]
    public string? SysLocation { get; set; }
}
