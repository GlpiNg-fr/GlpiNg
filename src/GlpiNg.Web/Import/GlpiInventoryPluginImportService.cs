using System.Text.Json;
using GlpiNg.Modules.Abstractions.Import;
using GlpiNg.Modules.Deployment.Import;
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
        IProgress<GlpiImportProgress>? progress = null,
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
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.IpRanges, 0));
            await ImportIpRangesAsync(connection, tablePrefix, entityId, result, cancellationToken);
        }

        if (selection.ImportSnmpCredentials)
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.SnmpCredentials, 0));
            await ImportSnmpCredentialsAsync(connection, tablePrefix, entityId, result, cancellationToken);
        }

        if (selection.ImportDeployPackages)
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.DeployPackages, 0));
            await ImportDeployPackagesAsync(connection, tablePrefix, entityId, result, cancellationToken);
        }

        if (selection.ImportUnmanagedDevices)
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.UnmanagedDevices, 0));
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

        // La colonne "json" porte le contenu du paquet (vérifications, actions, interactions).
        // Elle n'existe pas sur toutes les versions du plugin : sans elle, on retombe sur l'import
        // de la seule coquille, comme avant.
        string sql = $"SELECT id, name, {ColumnOrNull(columns, "comment")}, {ColumnOrNull(columns, "json")} FROM `{table}`";

        Dictionary<string, GlpiDeployFileInfo> knownFiles = await LoadDeployFilesAsync(connection, prefix, ct);
        List<(string Package, GlpiDeployPackageContent Content)> imported = [];

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            int sourceId = reader.GetInt32("id");
            string name = GetNullableString(reader, "name") ?? $"Paquet #{sourceId}";

            GlpiDeployPackageContent content = GlpiDeployPackageJsonMapper.Parse(GetNullableString(reader, "json"));

            DeploymentPackage? existing = await db.DeploymentPackages.IgnoreQueryFilters()
                .FirstOrDefaultAsync(package => package.SourceGlpiId == sourceId, ct);

            if (existing is null)
            {
                DeploymentPackage created = new()
                {
                    Name = name,
                    Description = GetNullableString(reader, "comment"),
                    SourceGlpiId = sourceId,
                    EntityId = entityId,
                };
                ApplyContent(created, content);
                ApplyFiles(created, content, knownFiles, result);
                db.DeploymentPackages.Add(created);
                result.DeployPackagesCreated++;
            }
            else
            {
                existing.Name = name;
                existing.Description = GetNullableString(reader, "comment") ?? existing.Description;

                // Un second passage réécrit le contenu depuis la source, comme le reste de
                // l'import : c'est ce qui le rend idempotent. Un paquet vide côté GLPI n'écrase
                // rien, pour ne pas effacer un contenu saisi ici entre deux imports.
                if (!content.IsEmpty)
                {
                    ApplyContent(existing, content);

                    await db.Entry(existing).Collection(package => package.Files).LoadAsync(ct);
                    ApplyFiles(existing, content, knownFiles, result);
                }

                result.DeployPackagesUpdated++;
            }

            result.DeployPackageChecksImported += content.Checks.Count;
            result.DeployPackageActionsImported += content.Actions.Count;

            if (content.Files.Count > 0 || content.Unsupported.Count > 0)
            {
                imported.Add((name, content));
            }
        }

        ReportPackageGaps(imported, result);
    }

    /// <summary>Ce que la table des fichiers du plugin sait d'un fichier, quand elle existe.</summary>
    private sealed record GlpiDeployFileInfo(string? Name, long SizeBytes);

    /// <summary>
    /// Lit le répertoire de fichiers du plugin (<c>deployfiles</c>) : nom d'origine et taille, par
    /// empreinte.
    ///
    /// L'association fichier ↔ paquet, elle, ne vient pas de cette table mais du document JSON du
    /// paquet — c'est ainsi que le plugin la stocke. Cette lecture ne sert qu'à compléter ce que le
    /// document ne dit pas toujours : la taille, et le nom quand il y manque. Table absente ou
    /// colonnes différentes selon la version : on se contente alors de ce que le document porte.
    ///
    /// Les deux colonnes d'empreinte sont indexées, le document pouvant référencer indifféremment
    /// l'empreinte complète ou sa forme courte selon la version du plugin.
    /// </summary>
    private static async Task<Dictionary<string, GlpiDeployFileInfo>> LoadDeployFilesAsync(
        MySqlConnection connection, string prefix, CancellationToken ct)
    {
        Dictionary<string, GlpiDeployFileInfo> files = new(StringComparer.OrdinalIgnoreCase);

        string table = prefix + "deployfiles";
        HashSet<string> columns = await GetColumnsAsync(connection, table, ct);

        if (columns.Count == 0)
        {
            return files;
        }

        string sql = $"SELECT {ColumnOrNull(columns, "name")}, {ColumnOrNull(columns, "filesize")}, "
            + $"{ColumnOrNull(columns, "sha512")}, {ColumnOrNull(columns, "shortsha512")} FROM `{table}`";

        await foreach (MySqlDataReader reader in ReadAsync(connection, sql, ct))
        {
            string? name = GetNullableString(reader, "name");
            long size = long.TryParse(GetNullableString(reader, "filesize"), out long parsed) ? parsed : 0;
            GlpiDeployFileInfo info = new(name, size);

            foreach (string column in (string[])["sha512", "shortsha512"])
            {
                if (GetNullableString(reader, column) is { Length: > 0 } hash)
                {
                    files[hash] = info;
                }
            }
        }

        return files;
    }

    /// <summary>
    /// Crée les fichiers qu'un paquet référence, sans leur contenu.
    ///
    /// Les octets vivent sur le disque du serveur GLPI, qu'un import lisant la base ne peut pas
    /// atteindre : le fichier est donc créé avec son nom, son empreinte et sa taille, mais sans
    /// aucun fragment. C'est un état volontairement visible — la fiche du paquet l'affiche « à
    /// téléverser », et <c>DeploymentTaskLaunchService</c> refuse de lancer un paquet dans cet
    /// état plutôt que d'envoyer à l'agent un job qu'il ne pourra pas terminer.
    ///
    /// Un fichier déjà présent n'est jamais réécrit au point de perdre ses fragments : un second
    /// import ne doit pas effacer un contenu que l'administrateur a téléversé entre-temps.
    /// </summary>
    private static void ApplyFiles(
        DeploymentPackage package,
        GlpiDeployPackageContent content,
        Dictionary<string, GlpiDeployFileInfo> knownFiles,
        GlpiPluginImportResult result)
    {
        foreach (GlpiDeployFileReference reference in content.Files)
        {
            GlpiDeployFileInfo? known = knownFiles.GetValueOrDefault(reference.Sha512);
            string fileName = reference.FileName ?? known?.Name ?? $"fichier-{reference.Sha512[..Math.Min(12, reference.Sha512.Length)]}";
            long size = known?.SizeBytes ?? 0;

            DeploymentPackageFile? existing = package.Files
                .FirstOrDefault(file => string.Equals(file.Sha512, reference.Sha512, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                package.Files.Add(new DeploymentPackageFile
                {
                    FileName = fileName,
                    Sha512 = reference.Sha512,
                    SizeBytes = size,
                });
                result.DeployPackageFilesCreated++;
            }
            else
            {
                existing.FileName = fileName;
                if (size > 0)
                {
                    existing.SizeBytes = size;
                }

                result.DeployPackageFilesUpdated++;
            }
        }
    }

    private static void ApplyContent(DeploymentPackage package, GlpiDeployPackageContent content)
    {
        package.ChecksJson = JsonSerializer.Serialize(content.Checks);
        package.ActionsJson = JsonSerializer.Serialize(content.Actions);
        package.UserInteractionsJson = JsonSerializer.Serialize(content.UserInteractions);
    }

    /// <summary>
    /// Dit précisément ce que l'import n'a pas pu reprendre, paquet par paquet.
    ///
    /// Les fichiers d'un paquet vivent sur le disque du serveur GLPI, hors de portée d'un import
    /// qui ne lit que la base. Ils ne sont volontairement pas créés en base ici : un paquet dont
    /// les fichiers sont déclarés mais absents produirait un job que l'agent ne peut pas terminer,
    /// et l'échec ne se verrait qu'au déploiement. Mieux vaut un paquet visiblement incomplet
    /// qu'un paquet qui semble prêt. Les noms et empreintes sont donc listés ici, pour que l'admin
    /// sache exactement quoi téléverser.
    /// </summary>
    private static void ReportPackageGaps(
        List<(string Package, GlpiDeployPackageContent Content)> imported, GlpiPluginImportResult result)
    {
        const int MaxDetailedPackages = 20;

        List<(string Package, GlpiDeployPackageContent Content)> withFiles =
            [.. imported.Where(entry => entry.Content.Files.Count > 0)];

        if (withFiles.Count > 0)
        {
            result.DeployPackageFilesPending = withFiles.Sum(entry => entry.Content.Files.Count);
            result.Warnings.Add(
                $"Paquets de déploiement : {result.DeployPackageFilesPending} fichier(s) créé(s) sur {withFiles.Count} paquet(s), sans leur contenu — à téléverser depuis la fiche du paquet. "
                + "Les octets vivent sur le disque du serveur GLPI, que cet import ne lit pas. Tant qu'ils manquent, ces paquets ne peuvent pas être déployés.");

            foreach ((string package, GlpiDeployPackageContent content) in withFiles.Take(MaxDetailedPackages))
            {
                string files = string.Join(", ", content.Files.Select(file =>
                    file.FileName is { Length: > 0 } named ? named : $"(sans nom, {file.Sha512[..Math.Min(12, file.Sha512.Length)]}…)"));
                result.Warnings.Add($"  « {package} » : {files}");
            }

            if (withFiles.Count > MaxDetailedPackages)
            {
                result.Warnings.Add($"  … et {withFiles.Count - MaxDetailedPackages} autre(s) paquet(s).");
            }
        }

        List<string> unsupported = [.. imported
            .SelectMany(entry => entry.Content.Unsupported)
            .GroupBy(label => label, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .Select(group => $"{group.Key} ×{group.Count()}")];

        if (unsupported.Count > 0)
        {
            result.Warnings.Add(
                "Paquets de déploiement : éléments sans équivalent dans GlpiNg, à recréer à la main — "
                + string.Join(", ", unsupported.Take(10))
                + (unsupported.Count > 10 ? ", …" : string.Empty));
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
