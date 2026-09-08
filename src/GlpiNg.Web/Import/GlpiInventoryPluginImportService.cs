using GlpiNg.Modules.Abstractions.Import;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Services;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GlpiNg.Web.Import;

/// <summary>
/// Implémentation de <see cref="IGlpiInventoryPluginImportService"/> : reprend, en lecture seule et
/// de façon idempotente, les données du plugin d'inventaire de la base GLPI source vers les
/// équivalents GlpiNg du module Déploiement. Pendant de <see cref="GlpiAdminMySqlImportService"/>
/// pour le domaine réseau/déploiement, et hébergé ici pour la même raison : seul l'hôte voit à la
/// fois les modèles du module Déploiement et la résolution d'entité.
///
/// Idempotence par <c>SourceGlpiId</c>, comme le reste de l'import. Les actifs non gérés font
/// exception : ils sont corrélés par adresse MAC puis IP, comme le fait déjà la découverte réseau
/// (voir <c>NetworkDeviceImportService</c>), pour qu'un équipement déjà repéré par un scan GlpiNg
/// ne soit pas dupliqué par l'import.
///
/// Les noms de colonnes varient entre FusionInventory et GLPI Inventory, et d'une version à
/// l'autre : chaque lecture passe par les colonnes réellement présentes (information_schema), même
/// principe que <c>GlpiMySqlImportService</c>.
/// </summary>
public sealed class GlpiInventoryPluginImportService(GlpiNgDbContext db, EntityTreeCache entityTree)
    : IGlpiInventoryPluginImportService
{
    public async Task<GlpiPluginImportResult> RunAsync(
        string connectionString,
        string tablePrefix,
        GlpiPluginImportSelection selection,
        CancellationToken cancellationToken = default)
    {
        GlpiPluginImportResult result = new();

        if (!selection.AnySelected)
        {
            return result;
        }

        await using MySqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Les objets importés arrivent hors session applicative (l'import tourne dans un scope
        // créé par GlpiImportStateService, qui est un singleton) : il n'y a pas d'entité active à
        // reprendre. Ils sont déposés dans l'entité racine, comme les postes remontés par un
        // agent, à charge pour un administrateur de les réaffecter.
        int? entityId = entityTree.GetRootEntityId();

        if (selection.ImportIpRanges)
        {
            await ImportIpRangesAsync(connection, tablePrefix, entityId, result, cancellationToken);
        }

        if (selection.ImportSnmpCredentials)
        {
            await ImportSnmpCredentialsAsync(connection, tablePrefix, entityId, result, cancellationToken);
        }

        if (selection.ImportDeployPackages)
        {
            await ImportDeployPackagesAsync(connection, tablePrefix, entityId, result, cancellationToken);
        }

        if (selection.ImportUnmanagedDevices)
        {
            await ImportUnmanagedDevicesAsync(connection, tablePrefix, entityId, result, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task ImportIpRangesAsync(
        MySqlConnection connection, string prefix, int? entityId, GlpiPluginImportResult result, CancellationToken ct)
    {
        string table = prefix + "ipranges";
        HashSet<string> columns = await GetColumnsAsync(connection, table, ct);

        if (columns.Count == 0)
        {
            result.Warnings.Add($"Table « {table} » absente : aucune plage IP reprise.");
            return;
        }

        // Les bornes ont porté plusieurs noms selon les versions du plugin.
        string? startColumn = FirstPresent(columns, "ip_start", "ip_begin", "begin_ip");
        string? endColumn = FirstPresent(columns, "ip_end", "end_ip");

        if (startColumn is null || endColumn is null)
        {
            result.Warnings.Add($"Colonnes de bornes introuvables dans « {table} » : aucune plage IP reprise.");
            return;
        }

        string sql = $"SELECT id, name, `{startColumn}` AS ip_start, `{endColumn}` AS ip_end FROM `{table}`";

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            int sourceId = reader.GetInt32("id");
            string? start = GetNullableString(reader, "ip_start");
            string? end = GetNullableString(reader, "ip_end");

            if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
            {
                continue;
            }

            IpRange? existing = await db.IpRanges.IgnoreQueryFilters()
                .FirstOrDefaultAsync(range => range.SourceGlpiId == sourceId, ct);

            if (existing is null)
            {
                db.IpRanges.Add(new IpRange
                {
                    Name = GetNullableString(reader, "name") ?? $"{start} → {end}",
                    StartIp = start,
                    EndIp = end,
                    SourceGlpiId = sourceId,
                    EntityId = entityId,
                });
                result.IpRangesCreated++;
            }
            else
            {
                existing.Name = GetNullableString(reader, "name") ?? existing.Name;
                existing.StartIp = start;
                existing.EndIp = end;
                result.IpRangesUpdated++;
            }
        }
    }

    private async Task ImportSnmpCredentialsAsync(
        MySqlConnection connection, string prefix, int? entityId, GlpiPluginImportResult result, CancellationToken ct)
    {
        string table = prefix + "configsecurities";
        HashSet<string> columns = await GetColumnsAsync(connection, table, ct);

        if (columns.Count == 0)
        {
            result.Warnings.Add($"Table « {table} » absente : aucun identifiant SNMP repris.");
            return;
        }

        string sql = $"""
            SELECT id, name,
                   {ColumnOrNull(columns, "snmpversion")},
                   {ColumnOrNull(columns, "community")},
                   {ColumnOrNull(columns, "username")},
                   {ColumnOrNull(columns, "auth_passphrase")},
                   {ColumnOrNull(columns, "encryption_passphrase")}
            FROM `{table}`
            """;

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            int sourceId = reader.GetInt32("id");

            SnmpCredential? existing = await db.SnmpCredentials.IgnoreQueryFilters()
                .FirstOrDefaultAsync(credential => credential.SourceGlpiId == sourceId, ct);

            SnmpCredential target = existing ?? new SnmpCredential
            {
                Name = GetNullableString(reader, "name") ?? $"Identifiant SNMP #{sourceId}",
                SourceGlpiId = sourceId,
                EntityId = entityId,
            };

            target.Name = GetNullableString(reader, "name") ?? target.Name;
            target.Version = ParseSnmpVersion(GetNullableString(reader, "snmpversion"));
            target.Community = GetNullableString(reader, "community") ?? target.Community;
            target.Username = GetNullableString(reader, "username") ?? target.Username;
            target.AuthPassphrase = GetNullableString(reader, "auth_passphrase") ?? target.AuthPassphrase;
            target.PrivPassphrase = GetNullableString(reader, "encryption_passphrase") ?? target.PrivPassphrase;

            if (existing is null)
            {
                db.SnmpCredentials.Add(target);
                result.SnmpCredentialsCreated++;
            }
            else
            {
                result.SnmpCredentialsUpdated++;
            }
        }

        // Les protocoles d'authentification/chiffrement SNMPv3 sont stockés côté plugin sous des
        // formes qui ont changé d'une version à l'autre (identifiant numérique, puis libellé) :
        // plutôt que de risquer une correspondance fausse sur un réglage de sécurité, ils sont
        // laissés à leur valeur par défaut et signalés.
        if (result.SnmpCredentialsCreated + result.SnmpCredentialsUpdated > 0)
        {
            result.Warnings.Add("Identifiants SNMP : les protocoles d'authentification et de chiffrement SNMPv3 ne sont pas repris et restent à « Aucun » — à vérifier sur chaque identifiant en v3.");
        }
    }

    private async Task ImportDeployPackagesAsync(
        MySqlConnection connection, string prefix, int? entityId, GlpiPluginImportResult result, CancellationToken ct)
    {
        string table = prefix + "deploypackages";
        HashSet<string> columns = await GetColumnsAsync(connection, table, ct);

        if (columns.Count == 0)
        {
            result.Warnings.Add($"Table « {table} » absente : aucun paquet de déploiement repris.");
            return;
        }

        string sql = $"SELECT id, name, {ColumnOrNull(columns, "comment")} FROM `{table}`";

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            int sourceId = reader.GetInt32("id");

            DeploymentPackage? existing = await db.DeploymentPackages.IgnoreQueryFilters()
                .FirstOrDefaultAsync(package => package.SourceGlpiId == sourceId, ct);

            if (existing is null)
            {
                db.DeploymentPackages.Add(new DeploymentPackage
                {
                    Name = GetNullableString(reader, "name") ?? $"Paquet #{sourceId}",
                    Description = GetNullableString(reader, "comment"),
                    SourceGlpiId = sourceId,
                    EntityId = entityId,
                });
                result.DeployPackagesCreated++;
            }
            else
            {
                existing.Name = GetNullableString(reader, "name") ?? existing.Name;
                existing.Description = GetNullableString(reader, "comment") ?? existing.Description;
                result.DeployPackagesUpdated++;
            }
        }

        if (result.DeployPackagesCreated + result.DeployPackagesUpdated > 0)
        {
            // Le contenu d'un paquet (vérifications, actions, interactions) est un document JSON
            // propre au plugin, et ses fichiers vivent sur le disque du serveur GLPI, hors de
            // portée d'un import qui ne lit que la base. Seule la coquille est reprise.
            result.Warnings.Add("Paquets de déploiement : seuls le nom et le commentaire sont repris. Les vérifications, actions et fichiers associés restent à recréer — ils vivent hors de la base GLPI.");
        }
    }

    private async Task ImportUnmanagedDevicesAsync(
        MySqlConnection connection, string prefix, int? entityId, GlpiPluginImportResult result, CancellationToken ct)
    {
        string table = prefix + "unmanageds";
        HashSet<string> columns = await GetColumnsAsync(connection, table, ct);

        if (columns.Count == 0)
        {
            result.Warnings.Add($"Table « {table} » absente : aucun actif non géré repris.");
            return;
        }

        string sql = $"""
            SELECT id, name,
                   {ColumnOrNull(columns, "ip")},
                   {ColumnOrNull(columns, "mac")},
                   {ColumnOrNull(columns, "sysdescr")},
                   {ColumnOrNull(columns, "domain")}
            FROM `{table}`
            """;

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            string? ip = GetNullableString(reader, "ip");
            string? mac = GetNullableString(reader, "mac");

            // Sans adresse, l'entrée n'est corrélable ni exploitable par une tâche réseau.
            if (string.IsNullOrWhiteSpace(ip) && string.IsNullOrWhiteSpace(mac))
            {
                continue;
            }

            // Même corrélation que la découverte réseau de GlpiNg : par MAC quand elle existe
            // (elle survit à un changement d'IP), sinon par IP.
            DiscoveredNetworkDevice? existing = await db.DiscoveredNetworkDevices.IgnoreQueryFilters()
                .FirstOrDefaultAsync(device => !string.IsNullOrEmpty(mac)
                    ? device.MacAddress == mac
                    : device.IpAddress == ip, ct);

            if (existing is null)
            {
                db.DiscoveredNetworkDevices.Add(new DiscoveredNetworkDevice
                {
                    IpAddress = ip ?? mac!,
                    MacAddress = mac,
                    Hostname = GetNullableString(reader, "name"),
                    SysDescr = GetNullableString(reader, "sysdescr"),
                    EntityId = entityId,
                });
                result.UnmanagedDevicesCreated++;
            }
            else
            {
                existing.Hostname = GetNullableString(reader, "name") ?? existing.Hostname;
                existing.SysDescr = GetNullableString(reader, "sysdescr") ?? existing.SysDescr;
                existing.MacAddress = mac ?? existing.MacAddress;
                result.UnmanagedDevicesUpdated++;
            }
        }
    }

    private static SnmpVersion ParseSnmpVersion(string? raw) => raw?.Trim() switch
    {
        // Le plugin stocke tantôt le numéro de version, tantôt son libellé.
        "1" or "v1" => SnmpVersion.V1,
        "3" or "v3" => SnmpVersion.V3,
        _ => SnmpVersion.V2c,
    };

    /// <summary>Colonne si elle existe, sinon un NULL constant sous le même alias — le lecteur n'a pas à savoir laquelle des deux.</summary>
    private static string ColumnOrNull(HashSet<string> columns, string column)
        => columns.Contains(column) ? $"`{column}`" : $"NULL AS `{column}`";

    private static string? FirstPresent(HashSet<string> columns, params string[] candidates)
        => candidates.FirstOrDefault(columns.Contains);

    private static async Task<HashSet<string>> GetColumnsAsync(MySqlConnection connection, string table, CancellationToken ct)
    {
        HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
        const string sql = "SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @table";

        await using MySqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@table", table);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async IAsyncEnumerable<MySqlDataReader> ReadAsync(
        MySqlConnection connection, string sql, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using MySqlCommand command = new(sql, connection);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            yield return reader;
        }
    }

    private static string? GetNullableString(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        string value = reader.GetValue(ordinal).ToString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
