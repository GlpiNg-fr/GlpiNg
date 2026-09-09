using System.Runtime.CompilerServices;
using GlpiNg.Modules.Abstractions.Import;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GlpiNg.Web.Import;

/// <summary>
/// Implémentation de <see cref="IGlpiAdminImportService"/> : importe, en lecture seule et de
/// façon idempotente, les entités/groupes/profils/utilisateurs GLPI (menu "Administration")
/// ainsi qu'un sous-ensemble de la configuration générale (glpi_configs, context "core") depuis
/// une base GLPI MySQL source — pendant de <c>GlpiMySqlImportService</c> (module Inventory) pour
/// le parc. Vit dans l'hôte (et non dans le module Inventory) car GlpiEntity/GlpiGroup/
/// GlpiProfile/GlpiUser sont des modèles de GlpiNg.Web, pas du module Inventory (voir la doc de
/// <see cref="IGlpiAdminImportService"/>).
///
/// Idempotence : une entité/un groupe/un profil/un utilisateur déjà importé (identifié par son
/// id GLPI d'origine, stocké dans <c>SourceGlpiId</c>) est mis à jour plutôt que dupliqué — même
/// principe que <see cref="Modules.Inventory.Models.Computer.SourceGlpiId"/>. Les habilitations
/// (glpi_profiles_users) et appartenances aux groupes (glpi_groups_users) d'un utilisateur sont
/// remplacées en bloc à chaque import (suppression puis réinsertion), comme les composants
/// matériels d'un poste côté import Inventory.
///
/// Points volontairement non couverts : les droits fins par module d'un profil GLPI
/// (glpi_profilerights) ne sont pas traduits vers les 6 <see cref="ProfileRightLevel"/> de
/// <see cref="GlpiProfile"/> — l'écart de granularité est trop important pour une correspondance
/// fiable ; seuls nom/commentaire/défaut sont repris, à configurer manuellement ensuite (voir
/// l'avertissement ajouté au résultat). La configuration générale importée se limite aux clés
/// glpi_configs ayant un équivalent direct et confirmé dans <see cref="GeneralSettings"/> (voir
/// les commentaires XML de ce modèle) ; le reste de glpi_configs (des centaines de clés, souvent
/// spécifiques à des modules absents de GlpiNg) est ignoré plutôt que mal interprété.
/// </summary>
public class GlpiAdminMySqlImportService(GlpiNgDbContext db, SettingsCacheService settingsStore, ConfigHistoryService history) : IGlpiAdminImportService
{
    private static readonly PasswordHasher<GlpiUser> PasswordHasher = new();

    public async Task<GlpiAdminImportAnalysis> AnalyzeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        return new GlpiAdminImportAnalysis
        {
            EntitiesCount = await TryCountAsync(connection, "SELECT COUNT(*) FROM glpi_entities", cancellationToken),
            GroupsCount = await TryCountAsync(connection, "SELECT COUNT(*) FROM glpi_groups", cancellationToken),
            ProfilesCount = await TryCountAsync(connection, "SELECT COUNT(*) FROM glpi_profiles", cancellationToken),
            UsersCount = await TryCountAsync(connection, "SELECT COUNT(*) FROM glpi_users", cancellationToken),
            GeneralConfigAvailable = await TryCountAsync(connection, "SELECT COUNT(*) FROM glpi_configs WHERE context = 'core'", cancellationToken) > 0,
        };
    }

    public async Task<GlpiAdminImportResult> RunAsync(
        string connectionString,
        GlpiAdminImportSelection selection,
        IProgress<GlpiImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        GlpiAdminImportResult result = new();

        await using MySqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);

        Dictionary<int, int> entityIdMap = [];
        Dictionary<int, int> groupIdMap = [];
        Dictionary<int, int> profileIdMap = [];

        if (selection.ImportEntities)
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.Entities, 0));

            try
            {
                entityIdMap = await ImportEntitiesAsync(connection, result, cancellationToken);
            }
            catch (MySqlException ex)
            {
                result.Warnings.Add($"Import des entités ignoré : {ex.Message}");
            }
        }
        else if (selection.ImportUsers)
        {
            entityIdMap = await db.Entities.Where(e => e.SourceGlpiId != null)
                .ToDictionaryAsync(e => e.SourceGlpiId!.Value, e => e.Id, cancellationToken);
        }

        if (selection.ImportGroups)
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.Groups, 0));

            try
            {
                groupIdMap = await ImportGroupsAsync(connection, result, cancellationToken);
            }
            catch (MySqlException ex)
            {
                result.Warnings.Add($"Import des groupes ignoré : {ex.Message}");
            }
        }
        else if (selection.ImportUsers)
        {
            groupIdMap = await db.Groups.Where(g => g.SourceGlpiId != null)
                .ToDictionaryAsync(g => g.SourceGlpiId!.Value, g => g.Id, cancellationToken);
        }

        if (selection.ImportProfiles)
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.Profiles, 0));

            try
            {
                profileIdMap = await ImportProfilesAsync(connection, result, cancellationToken);
            }
            catch (MySqlException ex)
            {
                result.Warnings.Add($"Import des profils ignoré : {ex.Message}");
            }
        }
        else if (selection.ImportUsers)
        {
            profileIdMap = await db.Profiles.Where(p => p.SourceGlpiId != null)
                .ToDictionaryAsync(p => p.SourceGlpiId!.Value, p => p.Id, cancellationToken);
        }

        if (selection.ImportUsers)
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.Users, 0));

            try
            {
                await ImportUsersAsync(connection, entityIdMap, groupIdMap, profileIdMap, result, cancellationToken);
            }
            catch (MySqlException ex)
            {
                result.Warnings.Add($"Import des utilisateurs ignoré : {ex.Message}");
            }
        }

        if (selection.ImportGeneralConfig)
        {
            progress?.Report(new GlpiImportProgress(GlpiImportPhases.GeneralConfig, 0));

            try
            {
                result.GeneralConfigKeysImported = await ImportGeneralConfigAsync(connection, cancellationToken);
            }
            catch (MySqlException ex)
            {
                result.Warnings.Add($"Import de la configuration générale ignoré : {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Première passe : crée/met à jour toutes les entités sans poser <see cref="GlpiEntity.ParentId"/>
    /// (l'id local d'une entité pas encore vue ne serait pas connu). Seconde passe : pose les
    /// ParentId une fois tous les id locaux connus. L'entité racine GLPI a <c>entities_id == id</c>
    /// (auto-référencée) : traitée comme "sans parent" plutôt que de créer une boucle.
    /// </summary>
    private async Task<Dictionary<int, int>> ImportEntitiesAsync(MySqlConnection connection, GlpiAdminImportResult result, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, comment, entities_id, address, postcode, town, state, country, website,
                   phone, fax, email, registration_number, longitude, latitude, altitude
            FROM glpi_entities
            """;

        List<GlpiEntityRow> rows = [];
        await using (MySqlCommand command = new(sql, connection))
        await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new GlpiEntityRow(
                    reader.GetInt32("id"),
                    reader.IsDBNull(reader.GetOrdinal("name")) ? "(sans nom)" : reader.GetString("name"),
                    ReadNullableString(reader, "comment"),
                    reader.GetInt32("entities_id"),
                    ReadNullableString(reader, "address"),
                    ReadNullableString(reader, "postcode"),
                    ReadNullableString(reader, "town"),
                    ReadNullableString(reader, "state"),
                    ReadNullableString(reader, "country"),
                    ReadNullableString(reader, "website"),
                    ReadNullableString(reader, "phone"),
                    ReadNullableString(reader, "fax"),
                    ReadNullableString(reader, "email"),
                    ReadNullableString(reader, "registration_number"),
                    ReadNullableString(reader, "longitude"),
                    ReadNullableString(reader, "latitude"),
                    ReadNullableString(reader, "altitude")));
            }
        }

        Dictionary<int, int> idMap = [];

        foreach (GlpiEntityRow row in rows)
        {
            GlpiEntity? entity = await db.Entities.FirstOrDefaultAsync(e => e.SourceGlpiId == row.Id, cancellationToken);
            bool isNew = entity is null;
            entity ??= new GlpiEntity { Name = row.Name, SourceGlpiId = row.Id };

            entity.Name = row.Name;
            entity.Comment = row.Comment;
            entity.Address = row.Address;
            entity.Postcode = row.Postcode;
            entity.Town = row.Town;
            entity.State = row.State;
            entity.Country = row.Country;
            entity.Website = row.Website;
            entity.Phone = row.Phone;
            entity.Fax = row.Fax;
            entity.Email = row.Email;
            entity.Registration = row.RegistrationNumber;
            entity.Longitude = row.Longitude;
            entity.Latitude = row.Latitude;
            entity.Altitude = row.Altitude;
            entity.UpdatedAt = DateTime.UtcNow;

            if (isNew)
            {
                db.Entities.Add(entity);
                result.EntitiesCreated++;
            }
            else
            {
                result.EntitiesUpdated++;
            }

            await db.SaveChangesAsync(cancellationToken);
            idMap[row.Id] = entity.Id;
        }

        foreach (GlpiEntityRow row in rows)
        {
            if (row.ParentGlpiId == row.Id)
            {
                continue;
            }

            if (!idMap.TryGetValue(row.Id, out int localId) || !idMap.TryGetValue(row.ParentGlpiId, out int localParentId))
            {
                continue;
            }

            GlpiEntity entity = await db.Entities.FirstAsync(e => e.Id == localId, cancellationToken);
            entity.ParentId = localParentId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return idMap;
    }

    /// <summary>Même logique en deux passes que <see cref="ImportEntitiesAsync"/>, avec <c>groups_id</c> comme colonne de parent GLPI.</summary>
    private async Task<Dictionary<int, int>> ImportGroupsAsync(MySqlConnection connection, GlpiAdminImportResult result, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, comment, code, groups_id, is_recursive, is_requester, is_watcher,
                   is_assign, is_task, is_notify, is_itemgroup, is_usergroup
            FROM glpi_groups
            """;

        List<GlpiGroupRow> rows = [];
        await using (MySqlCommand command = new(sql, connection))
        await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new GlpiGroupRow(
                    reader.GetInt32("id"),
                    reader.IsDBNull(reader.GetOrdinal("name")) ? "(sans nom)" : reader.GetString("name"),
                    ReadNullableString(reader, "comment"),
                    ReadNullableString(reader, "code"),
                    reader.GetInt32("groups_id"),
                    reader.GetBoolean("is_recursive"),
                    reader.GetBoolean("is_requester"),
                    reader.GetBoolean("is_watcher"),
                    reader.GetBoolean("is_assign"),
                    reader.GetBoolean("is_task"),
                    reader.GetBoolean("is_notify"),
                    reader.GetBoolean("is_itemgroup"),
                    reader.GetBoolean("is_usergroup")));
            }
        }

        Dictionary<int, int> idMap = [];

        foreach (GlpiGroupRow row in rows)
        {
            GlpiGroup? group = await db.Groups.FirstOrDefaultAsync(g => g.SourceGlpiId == row.Id, cancellationToken);
            bool isNew = group is null;
            group ??= new GlpiGroup { Name = row.Name, SourceGlpiId = row.Id };

            group.Name = row.Name;
            group.Comment = row.Comment;
            group.Code = row.Code;
            group.IsRecursive = row.IsRecursive;
            group.VisibleAsRequester = row.IsRequester;
            group.VisibleAsObserver = row.IsWatcher;
            group.VisibleAsAssignee = row.IsAssign;
            group.VisibleAsTask = row.IsTask;
            group.CanBeNotified = row.IsNotify;
            group.CanContainItems = row.IsItemGroup;
            group.CanContainUsers = row.IsUserGroup;
            group.UpdatedAt = DateTime.UtcNow;

            if (isNew)
            {
                db.Groups.Add(group);
                result.GroupsCreated++;
            }
            else
            {
                result.GroupsUpdated++;
            }

            await db.SaveChangesAsync(cancellationToken);
            idMap[row.Id] = group.Id;
        }

        foreach (GlpiGroupRow row in rows)
        {
            if (row.ParentGlpiId == 0 || row.ParentGlpiId == row.Id)
            {
                continue;
            }

            if (!idMap.TryGetValue(row.Id, out int localId) || !idMap.TryGetValue(row.ParentGlpiId, out int localParentId))
            {
                continue;
            }

            GlpiGroup group = await db.Groups.FirstAsync(g => g.Id == localId, cancellationToken);
            group.ParentId = localParentId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return idMap;
    }

    /// <summary>
    /// Seuls nom/commentaire/défaut sont importés : les droits fins par module d'un profil GLPI
    /// (glpi_profilerights) n'ont pas d'équivalent fiable dans les 6 <see cref="ProfileRightLevel"/>
    /// de <see cref="GlpiProfile"/> — voir la doc de tête de cette classe.
    /// </summary>
    private async Task<Dictionary<int, int>> ImportProfilesAsync(MySqlConnection connection, GlpiAdminImportResult result, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, comment, is_default FROM glpi_profiles";

        List<(int Id, string Name, string? Comment, bool IsDefault)> rows = [];
        await using (MySqlCommand command = new(sql, connection))
        await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((
                    reader.GetInt32("id"),
                    reader.IsDBNull(reader.GetOrdinal("name")) ? "(sans nom)" : reader.GetString("name"),
                    ReadNullableString(reader, "comment"),
                    reader.GetBoolean("is_default")));
            }
        }

        Dictionary<int, int> idMap = [];

        foreach ((int Id, string Name, string? Comment, bool IsDefault) row in rows)
        {
            GlpiProfile? profile = await db.Profiles.FirstOrDefaultAsync(p => p.SourceGlpiId == row.Id, cancellationToken);
            bool isNew = profile is null;
            profile ??= new GlpiProfile { Name = row.Name, SourceGlpiId = row.Id };

            profile.Name = row.Name;
            profile.Comment = row.Comment;
            profile.IsDefault = row.IsDefault;
            profile.UpdatedAt = DateTime.UtcNow;

            if (isNew)
            {
                db.Profiles.Add(profile);
                result.ProfilesCreated++;
            }
            else
            {
                result.ProfilesUpdated++;
            }

            await db.SaveChangesAsync(cancellationToken);
            idMap[row.Id] = profile.Id;
        }

        if (rows.Count > 0)
        {
            result.Warnings.Add(
                "Les droits fins des profils GLPI (glpi_profilerights) ne sont pas repris : seuls le nom, " +
                "le commentaire et le statut par défaut sont importés — à configurer manuellement pour chaque profil.");
        }

        return idMap;
    }

    /// <summary>
    /// Mot de passe : GLPI stocke un hash incompatible avec <see cref="PasswordHasher{TUser}"/>
    /// (utilisé par <c>UserCredentialAuthenticator</c>) — un utilisateur nouvellement importé
    /// reçoit un mot de passe aléatoire inutilisable (même technique que le provisionnement LDAP,
    /// voir <c>UserCredentialAuthenticator.AuthenticateLdapAsync</c>), à réinitialiser depuis
    /// /administration/utilisateurs. Un utilisateur déjà importé (SourceGlpiId connu) conserve son
    /// mot de passe GlpiNg existant : seuls profil/nom/coordonnées/habilitations sont mis à jour.
    /// </summary>
    private async Task ImportUsersAsync(
        MySqlConnection connection,
        Dictionary<int, int> entityIdMap,
        Dictionary<int, int> groupIdMap,
        Dictionary<int, int> profileIdMap,
        GlpiAdminImportResult result,
        CancellationToken cancellationToken)
    {
        string locationsLabelColumn = await PreferredLabelColumnAsync(connection, "glpi_locations", cancellationToken);
        Dictionary<int, string> locations = await LoadLookupAsync(connection, "glpi_locations", locationsLabelColumn, cancellationToken);

        const string sql = """
            SELECT u.id, u.name, u.realname, u.firstname, u.phone, u.is_active, u.locations_id,
                   (SELECT e.email FROM glpi_useremails e WHERE e.users_id = u.id ORDER BY e.is_default DESC, e.id ASC LIMIT 1) AS email
            FROM glpi_users u
            """;

        List<GlpiUserRow> rows = [];
        await using (MySqlCommand command = new(sql, connection))
        await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new GlpiUserRow(
                    reader.GetInt32("id"),
                    reader.IsDBNull(reader.GetOrdinal("name")) ? $"glpi-{reader.GetInt32("id")}" : reader.GetString("name"),
                    ReadNullableString(reader, "realname"),
                    ReadNullableString(reader, "firstname"),
                    ReadNullableString(reader, "phone"),
                    reader.IsDBNull(reader.GetOrdinal("is_active")) || reader.GetBoolean("is_active"),
                    ReadNullableInt(reader, "locations_id"),
                    ReadNullableString(reader, "email")));
            }
        }

        Dictionary<int, int> userIdMap = [];

        foreach (GlpiUserRow row in rows)
        {
            GlpiUser? existingUser = await db.Users.FirstOrDefaultAsync(u => u.SourceGlpiId == row.Id, cancellationToken);
            bool isNew = existingUser is null;
            GlpiUser user = existingUser ?? new GlpiUser { UserName = row.Name, PasswordHash = string.Empty, SourceGlpiId = row.Id };
            if (isNew)
            {
                user.PasswordHash = PasswordHasher.HashPassword(user, Guid.NewGuid().ToString("N"));
            }

            user.UserName = row.Name;
            user.LastName = row.RealName;
            user.FirstName = row.FirstName;
            user.DisplayName = string.Join(" ", new[] { row.FirstName, row.RealName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(user.DisplayName))
            {
                user.DisplayName = row.Name;
            }
            user.Phone = row.Phone;
            user.Email = row.Email;
            user.IsActive = row.IsActive;
            user.Location = row.LocationsId.HasValue && locations.TryGetValue(row.LocationsId.Value, out string? locationName)
                ? locationName
                : user.Location;

            if (isNew)
            {
                db.Users.Add(user);
                result.UsersCreated++;
            }
            else
            {
                result.UsersUpdated++;
            }

            await db.SaveChangesAsync(cancellationToken);
            userIdMap[row.Id] = user.Id;
        }

        await ImportHabilitationsAsync(connection, userIdMap, entityIdMap, profileIdMap, cancellationToken);
        await ImportGroupMembershipsAsync(connection, userIdMap, groupIdMap, cancellationToken);
    }

    /// <summary>glpi_profiles_users, remplacées en bloc par utilisateur (comme les composants matériels d'un poste côté import Inventory).</summary>
    private async Task ImportHabilitationsAsync(
        MySqlConnection connection,
        Dictionary<int, int> userIdMap,
        Dictionary<int, int> entityIdMap,
        Dictionary<int, int> profileIdMap,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT users_id, entities_id, profiles_id, is_recursive FROM glpi_profiles_users";

        Dictionary<int, List<(int EntitiesId, int ProfilesId, bool IsRecursive)>> byUser = [];
        await using (MySqlCommand command = new(sql, connection))
        await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                int usersId = reader.GetInt32("users_id");
                if (!byUser.TryGetValue(usersId, out List<(int, int, bool)>? list))
                {
                    list = [];
                    byUser[usersId] = list;
                }

                list.Add((reader.GetInt32("entities_id"), reader.GetInt32("profiles_id"), reader.GetBoolean("is_recursive")));
            }
        }

        foreach (KeyValuePair<int, List<(int EntitiesId, int ProfilesId, bool IsRecursive)>> entry in byUser)
        {
            if (!userIdMap.TryGetValue(entry.Key, out int localUserId))
            {
                continue;
            }

            List<GlpiUserProfile> existing = await db.UserProfiles.Where(up => up.UserId == localUserId).ToListAsync(cancellationToken);
            db.UserProfiles.RemoveRange(existing);

            foreach ((int EntitiesId, int ProfilesId, bool IsRecursive) row in entry.Value)
            {
                if (!entityIdMap.TryGetValue(row.EntitiesId, out int localEntityId) || !profileIdMap.TryGetValue(row.ProfilesId, out int localProfileId))
                {
                    continue;
                }

                db.UserProfiles.Add(new GlpiUserProfile
                {
                    UserId = localUserId,
                    EntityId = localEntityId,
                    ProfileId = localProfileId,
                    IsRecursive = row.IsRecursive,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>glpi_groups_users, remplacées en bloc par utilisateur.</summary>
    private async Task ImportGroupMembershipsAsync(
        MySqlConnection connection,
        Dictionary<int, int> userIdMap,
        Dictionary<int, int> groupIdMap,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT users_id, groups_id FROM glpi_groups_users";

        Dictionary<int, List<int>> byUser = [];
        await using (MySqlCommand command = new(sql, connection))
        await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                int usersId = reader.GetInt32("users_id");
                if (!byUser.TryGetValue(usersId, out List<int>? list))
                {
                    list = [];
                    byUser[usersId] = list;
                }

                list.Add(reader.GetInt32("groups_id"));
            }
        }

        foreach (KeyValuePair<int, List<int>> entry in byUser)
        {
            if (!userIdMap.TryGetValue(entry.Key, out int localUserId))
            {
                continue;
            }

            List<GlpiGroupUser> existing = await db.GroupUsers.Where(gu => gu.UserId == localUserId).ToListAsync(cancellationToken);
            db.GroupUsers.RemoveRange(existing);

            foreach (int glpiGroupId in entry.Value)
            {
                if (!groupIdMap.TryGetValue(glpiGroupId, out int localGroupId))
                {
                    continue;
                }

                db.GroupUsers.Add(new GlpiGroupUser { UserId = localUserId, GroupId = localGroupId });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ne mappe que les clés glpi_configs (context "core") ayant un équivalent direct et confirmé
    /// dans <see cref="GeneralSettings"/> (voir les commentaires XML de ce modèle, qui documentent
    /// déjà certaines de ces clés d'origine GLPI) — le reste de glpi_configs est ignoré plutôt que
    /// mal interprété.
    /// </summary>
    private async Task<int> ImportGeneralConfigAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        Dictionary<string, string?> configValues = new(StringComparer.Ordinal);
        const string sql = "SELECT name, value FROM glpi_configs WHERE context = 'core'";

        await using (MySqlCommand command = new(sql, connection))
        await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                configValues[reader.GetString("name")] = ReadNullableString(reader, "value");
            }
        }

        const string sectionName = "GeneralSettings";
        GeneralSettings before = await settingsStore.ReadSectionAsync<GeneralSettings>(sectionName, cancellationToken);
        GeneralSettings general = await settingsStore.ReadSectionAsync<GeneralSettings>(sectionName, cancellationToken);

        int applied = 0;

        if (configValues.TryGetValue("url_base", out string? urlBase) && !string.IsNullOrWhiteSpace(urlBase))
        {
            general.ApplicationUrl = urlBase;
            applied++;
        }

        if (configValues.TryGetValue("text_login", out string? textLogin))
        {
            general.LoginPageMessage = string.IsNullOrEmpty(textLogin) ? null : textLogin;
            applied++;
        }

        if (TryGetInt(configValues, "decimal_number", out int decimalNumber))
        {
            general.DefaultDecimalsCount = decimalNumber;
            applied++;
        }

        if (TryGetInt(configValues, "dropdown_max", out int dropdownMax))
        {
            general.DefaultDropdownListLimit = dropdownMax;
            applied++;
        }

        if (TryGetInt(configValues, "list_limit_max", out int listLimitMax))
        {
            general.MaxSearchResultsPerPage = listLimitMax;
            applied++;
        }

        if (TryGetInt(configValues, "login_remember_time", out int rememberTime))
        {
            general.RememberMeDuration = rememberTime;
            applied++;
        }

        if (TryGetBool(configValues, "use_public_faq", out bool publicFaq))
        {
            general.AllowAnonymousFaqAccess = publicFaq;
            applied++;
        }

        if (configValues.TryGetValue("helpdesk_doc_url", out string? helpdeskDocUrl))
        {
            general.SimplifiedInterfaceHelpLink = string.IsNullOrEmpty(helpdeskDocUrl) ? null : helpdeskDocUrl;
            applied++;
        }

        if (configValues.TryGetValue("central_doc_url", out string? centralDocUrl))
        {
            general.StandardInterfaceHelpLink = string.IsNullOrEmpty(centralDocUrl) ? null : centralDocUrl;
            applied++;
        }

        if (TryGetInt(configValues, "allow_search_view", out int searchView))
        {
            general.SearchViewMode = searchView;
            applied++;
        }

        if (TryGetInt(configValues, "allow_search_all", out int searchAll))
        {
            general.SearchAllListMode = searchAll;
            applied++;
        }

        if (applied > 0)
        {
            await settingsStore.SaveSectionAsync(sectionName, general, cancellationToken);
            await history.AppendAsync("Import GLPI", SettingsDiff.Compare(before, general), cancellationToken);
        }

        return applied;
    }

    private static bool TryGetInt(Dictionary<string, string?> values, string key, out int result)
    {
        result = 0;
        return values.TryGetValue(key, out string? raw) && int.TryParse(raw, out result);
    }

    private static bool TryGetBool(Dictionary<string, string?> values, string key, out bool result)
    {
        result = false;
        if (!values.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (raw is "1" or "0")
        {
            result = raw == "1";
            return true;
        }

        return bool.TryParse(raw, out result);
    }

    private static string? ReadNullableString(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal).ToString();
    }

    private static int? ReadNullableInt(MySqlDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static async Task<int> TryCountAsync(MySqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await using MySqlCommand command = new(sql, connection);
            object? value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null or DBNull ? 0 : Convert.ToInt32(value);
        }
        catch (MySqlException)
        {
            return 0;
        }
    }

    private static async Task<string> PreferredLabelColumnAsync(MySqlConnection connection, string table, CancellationToken cancellationToken)
    {
        HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
        const string sql = "SELECT column_name FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @table";

        await using MySqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue("@table", table);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns.Contains("completename") ? "completename" : "name";
    }

    private static async Task<Dictionary<int, string>> LoadLookupAsync(
        MySqlConnection connection, string table, string labelColumn, CancellationToken cancellationToken)
    {
        Dictionary<int, string> result = [];

        try
        {
            string sql = $"SELECT id, `{labelColumn}` FROM `{table}`";
            await using MySqlCommand command = new(sql, connection);
            await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                int id = reader.GetInt32(0);
                result[id] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }
        }
        catch (MySqlException)
        {
            // Table absente sur cette version de GLPI : lookup vide, les localisations restent non résolues.
        }

        return result;
    }

    private sealed record GlpiEntityRow(
        int Id,
        string Name,
        string? Comment,
        int ParentGlpiId,
        string? Address,
        string? Postcode,
        string? Town,
        string? State,
        string? Country,
        string? Website,
        string? Phone,
        string? Fax,
        string? Email,
        string? RegistrationNumber,
        string? Longitude,
        string? Latitude,
        string? Altitude);

    private sealed record GlpiGroupRow(
        int Id,
        string Name,
        string? Comment,
        string? Code,
        int ParentGlpiId,
        bool IsRecursive,
        bool IsRequester,
        bool IsWatcher,
        bool IsAssign,
        bool IsTask,
        bool IsNotify,
        bool IsItemGroup,
        bool IsUserGroup);

    private sealed record GlpiUserRow(
        int Id,
        string Name,
        string? RealName,
        string? FirstName,
        string? Phone,
        bool IsActive,
        int? LocationsId,
        string? Email);
}
