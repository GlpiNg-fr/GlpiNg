using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Agent;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Importe le contenu d'une requête "netdiscovery"/"netinventory" du protocole GLPI-Agent
/// (voir <see cref="NetworkInventoryContent"/>) dans <see cref="DiscoveredNetworkDevice"/> —
/// délibérément distinct de <see cref="InventoryImportService"/>, qui suppose une section
/// "hardware" et créerait un Computer "Inconnu" si on le réutilisait sur un payload de scan réseau
/// sans matériel (voir la doc de NetworkInventoryRequest).
/// </summary>
public class NetworkDeviceImportService(
    GlpiNgDbContext db,
    NotificationDispatchService notificationDispatch,
    IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// Upsert un lot de <see cref="DiscoveredNetworkDevice"/> à partir d'un résultat de scan.
    /// Corrélation avec un équipement déjà connu (voir la doc de DiscoveredNetworkDevice) : par
    /// <see cref="NetworkInventoryDevice.Mac"/> quand présente (survit à un changement d'IP DHCP
    /// entre deux scans), sinon par <see cref="NetworkInventoryDevice.Ip"/>.
    /// </summary>
    public async Task<int> ImportAsync(int? networkTaskId, NetworkInventoryContent content, CancellationToken cancellationToken = default)
    {
        int newDeviceCount = 0;

        foreach (NetworkInventoryDevice discovered in content.Devices)
        {
            if (string.IsNullOrWhiteSpace(discovered.Ip))
            {
                continue;
            }

            DiscoveredNetworkDevice? existing = null;
            if (!string.IsNullOrWhiteSpace(discovered.Mac))
            {
                existing = await db.DiscoveredNetworkDevices.FirstOrDefaultAsync(d => d.MacAddress == discovered.Mac, cancellationToken);
            }
            existing ??= await db.DiscoveredNetworkDevices.FirstOrDefaultAsync(d => d.IpAddress == discovered.Ip, cancellationToken);

            bool isNew = existing is null;
            DiscoveredNetworkDevice device = existing ?? new DiscoveredNetworkDevice { IpAddress = discovered.Ip };

            device.IpAddress = discovered.Ip;
            device.MacAddress = discovered.Mac ?? device.MacAddress;
            device.Hostname = discovered.Hostname ?? device.Hostname;
            device.SysDescr = discovered.SysDescr ?? device.SysDescr;
            device.SysName = discovered.SysName ?? device.SysName;
            device.SysContact = discovered.SysContact ?? device.SysContact;
            device.SysLocation = discovered.SysLocation ?? device.SysLocation;
            device.GuessedType = GuessType(device.SysDescr) ?? device.GuessedType;
            device.DiscoveredViaNetworkTaskId = networkTaskId ?? device.DiscoveredViaNetworkTaskId;
            device.LastSeenAt = DateTime.UtcNow;

            if (isNew)
            {
                device.FirstSeenAt = DateTime.UtcNow;
                db.DiscoveredNetworkDevices.Add(device);
                newDeviceCount++;
            }

            await db.SaveChangesAsync(cancellationToken);

            if (isNew)
            {
                await PublishNewDeviceNotificationAsync(device, cancellationToken);
            }
        }

        return newDeviceCount;
    }

    /// <summary>Heuristique très grossière à partir du sysDescr SNMP (mots-clés usuels des
    /// bannières constructeur) — purement indicatif, affiché sur DiscoveredDevices.razor comme
    /// aide au tri avant promotion manuelle, jamais utilisé pour peupler NetworkEquipment.Type
    /// automatiquement.</summary>
    private static string? GuessType(string? sysDescr)
    {
        if (string.IsNullOrWhiteSpace(sysDescr))
        {
            return null;
        }

        string lower = sysDescr.ToLowerInvariant();
        if (lower.Contains("switch")) return "Switch";
        if (lower.Contains("router") || lower.Contains("routeur")) return "Routeur";
        if (lower.Contains("printer") || lower.Contains("imprimante")) return "Imprimante";
        if (lower.Contains("access point") || lower.Contains("wireless")) return "Point d'accès";
        if (lower.Contains("firewall") || lower.Contains("pare-feu")) return "Pare-feu";

        return null;
    }

    private async Task PublishNewDeviceNotificationAsync(DiscoveredNetworkDevice device, CancellationToken cancellationToken)
    {
        Microsoft.AspNetCore.Http.HttpRequest? request = httpContextAccessor.HttpContext?.Request;
        string url = request is null
            ? "/tools/deployments/discovered-devices"
            : $"{request.Scheme}://{request.Host}/tools/deployments/discovered-devices";

        Dictionary<string, string?> variables = new()
        {
            ["device.ip"] = device.IpAddress,
            ["device.mac"] = device.MacAddress,
            ["device.hostname"] = device.Hostname,
            ["device.url"] = url,
        };

        await notificationDispatch.PublishAsync(
            NotificationEventCatalog.DiscoveredNetworkDevice, NotificationEventCatalog.EventNew, device.Id, variables, cancellationToken);
    }
}
