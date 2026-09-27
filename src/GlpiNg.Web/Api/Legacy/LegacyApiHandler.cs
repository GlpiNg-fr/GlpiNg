using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Modules.Abstractions.Preferences;
using GlpiNg.Web.Api.Glpi;
using GlpiNg.Web.Api.Glpi.Data;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Documents;
using GlpiNg.Web.Services;
using GlpiNg.Web.Services.Documents;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Api.Legacy;

/// <summary>
/// API REST « legacy » de GLPI (apirest.php, api.php/v1) : même routage, mêmes paramètres, mêmes
/// réponses et mêmes codes d'erreur que <c>Glpi\Api\APIRest</c> et <c>Glpi\Api\API</c>, dont chaque
/// méthode est reprise ici sous le même nom. Voir apirest.md (servi à la racine de l'API).
/// </summary>
public sealed class LegacyApiHandler(
    HttpContext http,
    GlpiApiAccess access,
    GlpiApiSessionStore sessions,
    SettingsCacheService settings)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>max_input_vars de PHP par défaut, annoncé par Accept-Range.</summary>
    private const int MaxInputVars = 1000;

    private static readonly string[] AllowedWithoutSession = ["initSession", "lostPassword"];

    private readonly CancellationToken _ct = http.RequestAborted;
    private JsonObject _params = [];
    private string _verb = "GET";
    private string[] _elements = [];
    private string _apiUrl = string.Empty;
    private GlpiApiSession? _session;
    private int? _clientId;
    private GlpiDataContext? _data;

    public static async Task HandleAsync(HttpContext http, string? path)
    {
        LegacyApiHandler handler = new(
            http,
            http.RequestServices.GetRequiredService<GlpiApiAccess>(),
            http.RequestServices.GetRequiredService<GlpiApiSessionStore>(),
            http.RequestServices.GetRequiredService<SettingsCacheService>());
        await handler.CallAsync(path ?? string.Empty);
    }

    // --- APIRest::call ------------------------------------------------------------------------

    private async Task CallAsync(string pathInfo)
    {
        try
        {
            _verb = http.Request.Method.ToUpperInvariant();
            PathString pathBase = http.Request.PathBase;
            _apiUrl = $"{http.Request.Scheme}://{http.Request.Host}{pathBase}/api.php/v1";

            string path = pathInfo.Replace("api/", string.Empty).Replace("v1/", string.Empty).Trim('/');
            if (path == "v1")
            {
                path = string.Empty;
            }
            _elements = path.Split('/');
            string resource = _elements[0].Trim();
            bool isInlineDoc = resource.Length == 0 || resource == "api";

            Cors();
            if (_verb == "OPTIONS")
            {
                http.Response.StatusCode = 200;
                return;
            }

            await ParseIncomingParamsAsync(isInlineDoc);

            // retrieveSession
            string? token = Str("session_token");
            if (!string.IsNullOrEmpty(token))
            {
                _session = sessions.Find(token);
            }

            // initApi
            ApiSettings apiSettings = await settings.ReadSectionAsync<ApiSettings>("ApiSettings", _ct);
            if (!apiSettings.EnableLegacyRestApi)
            {
                throw new LegacyApiException("ERROR", "API disabled", docMessage: false);
            }
            GlpiApiClientCheck client = await access.CheckClientAsync(http, Str("app_token"), _ct);
            if (client.ErrorCode == "ERROR_NOT_ALLOWED_IP")
            {
                throw new LegacyApiException(client.ErrorCode, client.ErrorMessage!, docMessage: false);
            }

            if (!isInlineDoc && !AllowedWithoutSession.Contains(resource))
            {
                await InitEndpointAsync(client, resource);
            }

            if (isInlineDoc)
            {
                await InlineDocumentationAsync();
                return;
            }

            switch (resource)
            {
                case "initSession":
                    await Respond(await InitSessionAsync(client, apiSettings));
                    return;
                case "killSession":
                    sessions.Remove(_session!.Token);
                    await Respond(JsonValue.Create(true));
                    return;
                case "changeActiveEntities":
                    await Respond(await ChangeActiveEntitiesAsync());
                    return;
                case "getMyEntities":
                    await Respond(await GetMyEntitiesAsync());
                    return;
                case "getActiveEntities":
                    await Respond(GetActiveEntities());
                    return;
                case "changeActiveProfile":
                    await Respond(ChangeActiveProfile());
                    return;
                case "getMyProfiles":
                    await Respond(await GetMyProfilesAsync());
                    return;
                case "getActiveProfile":
                    await Respond(new JsonObject { ["active_profile"] = await ActiveProfileAsync() });
                    return;
                case "getFullSession":
                    await Respond(new JsonObject { ["session"] = await FullSessionAsync() });
                    return;
                case "getGlpiConfig":
                    await Respond(new JsonObject { ["cfg_glpi"] = await GlpiConfigAsync() });
                    return;
                case "listSearchOptions":
                {
                    GlpiItemtypeInfo itemtype = GetItemtype(1, recursive: true)!;
                    await Respond(itemtype.SearchOptions.DeepClone());
                    return;
                }
                case "getMultipleItems":
                    await Respond(await GetMultipleItemsAsync());
                    return;
                case "search":
                    await SearchAsync();
                    return;
                case "lostPassword":
                    if (_verb is not ("PUT" or "PATCH"))
                    {
                        throw new LegacyApiException("ERROR", "Only HTTP verb PUT is allowed");
                    }
                    // GlpiNg n'a pas de réinitialisation de mot de passe par courriel.
                    throw new LegacyApiException("ERROR", "Email notifications are disabled");
                case "getMassiveActions":
                    await Respond(GetMassiveActions(GetItemtype(1, recursive: false)!, Element(2), Bool("is_deleted")));
                    return;
                case "getMassiveActionParameters":
                    await Respond(GetMassiveActionParameters(Element(2)));
                    return;
                case "applyMassiveAction":
                    await ApplyMassiveActionAsync();
                    return;
            }

            if (_elements.Length >= 3 && resource.Equals("User", StringComparison.OrdinalIgnoreCase)
                && _elements[2].Equals("Picture", StringComparison.OrdinalIgnoreCase))
            {
                // GlpiNg ne gère pas de photo de profil : réponse de GLPI pour un compte qui n'en a pas.
                http.Response.StatusCode = 204;
                return;
            }

            await CommonDbtmAsync();
        }
        catch (LegacyApiException e)
        {
            await ReturnError(e);
        }
        catch (JsonException)
        {
            await ReturnError(new LegacyApiException("ERROR_JSON_PAYLOAD_INVALID", "JSON payload seems not valid", docMessage: false));
        }
    }

    // --- Paramètres, session, en-têtes --------------------------------------------------------

    private void Cors()
    {
        if (http.Request.Headers.ContainsKey("Origin"))
        {
            http.Response.Headers["Access-Control-Allow-Origin"] = "*";
        }
        if (_verb is "GET" or "OPTIONS")
        {
            http.Response.Headers["Access-Control-Expose-Headers"] = "content-type, content-range, accept-range";
        }
        if (_verb == "OPTIONS")
        {
            if (http.Request.Headers.ContainsKey("Access-Control-Request-Method"))
            {
                http.Response.Headers["Access-Control-Allow-Methods"] = "PUT, GET, POST, DELETE, OPTIONS";
            }
            if (http.Request.Headers.ContainsKey("Access-Control-Request-Headers"))
            {
                http.Response.Headers["Access-Control-Allow-Headers"] = "origin, content-type, accept, session-token, authorization, app-token";
            }
        }
    }

    /// <summary>APIRest::parseIncomingParams.</summary>
    private async Task ParseIncomingParamsAsync(bool isInlineDoc)
    {
        JsonObject parameters = PhpParams.Parse(http.Request.QueryString.Value);
        string contentType = http.Request.ContentType ?? (isInlineDoc ? string.Empty : "application/json");

        if (contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            IFormCollection form = await http.Request.ReadFormAsync(_ct);
            if (form.Files.Count == 0)
            {
                throw new LegacyApiException("ERROR_UPLOAD_FILE_TOO_BIG_POST_MAX_SIZE", "The file seems too big", docMessage: false);
            }
            if (JsonNode.Parse(form["uploadManifest"].ToString()) is JsonObject manifest)
            {
                foreach ((string key, JsonNode? value) in manifest)
                {
                    parameters[key] = value?.DeepClone();
                }
            }
        }
        else
        {
            string body;
            using (StreamReader reader = new(http.Request.Body, Encoding.UTF8))
            {
                body = (await reader.ReadToEndAsync(_ct)).Trim();
            }
            if (body.Length > 0 && _verb == "GET")
            {
                throw new LegacyApiException("ERROR_JSON_PAYLOAD_FORBIDDEN", "GET Request should not have json payload (http body)");
            }
            if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                if (body.Length > 0)
                {
                    JsonNode? parsed;
                    try
                    {
                        parsed = JsonNode.Parse(body);
                    }
                    catch (JsonException)
                    {
                        throw new LegacyApiException("ERROR_JSON_PAYLOAD_INVALID", "JSON payload seems not valid", docMessage: false);
                    }
                    if (parsed is JsonObject obj)
                    {
                        foreach ((string key, JsonNode? value) in obj)
                        {
                            parameters[key] = value?.DeepClone();
                        }
                    }
                }
            }
            else if (contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                foreach ((string key, JsonNode? value) in PhpParams.Parse(body))
                {
                    parameters[key] = value?.DeepClone();
                }
            }
        }

        // Authentification HTTP Basic, jeton utilisateur, jetons de session et d'application.
        string authorization = http.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[6..].Trim()));
                int colon = decoded.IndexOf(':');
                if (colon >= 0)
                {
                    parameters["login"] = decoded[..colon];
                    parameters["password"] = decoded[(colon + 1)..];
                }
            }
            catch (FormatException)
            {
            }
        }
        if (authorization.Contains("user_token", StringComparison.Ordinal))
        {
            string[] parts = authorization.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                parameters["user_token"] = parts[1];
            }
        }
        if (http.Request.Headers.TryGetValue("Session-Token", out Microsoft.Extensions.Primitives.StringValues sessionToken))
        {
            parameters["session_token"] = sessionToken.ToString();
        }
        if (http.Request.Headers.TryGetValue("App-Token", out Microsoft.Extensions.Primitives.StringValues appToken))
        {
            parameters["app_token"] = appToken.ToString();
        }

        // Booléens « true » / « false » passés en texte.
        foreach (string key in parameters.Select(p => p.Key).ToList())
        {
            if (parameters[key] is JsonValue v && v.GetValueKind() == JsonValueKind.String)
            {
                string s = v.GetValue<string>();
                if (s == "true")
                {
                    parameters[key] = true;
                }
                else if (s == "false")
                {
                    parameters[key] = false;
                }
            }
        }
        _params = parameters;
    }

    /// <summary>API::initEndpoint : jeton d'application, journalisation, jeton de session.</summary>
    private async Task InitEndpointAsync(GlpiApiClientCheck client, string endpoint)
    {
        CheckAppToken(client);
        await access.LogEndpointAsync(http, _clientId, endpoint, _session?.UserName, _ct);

        if (string.IsNullOrEmpty(Str("session_token")))
        {
            throw new LegacyApiException("ERROR_SESSION_TOKEN_MISSING", "parameter session_token is missing or empty");
        }
        if (_session is null)
        {
            throw new LegacyApiException("ERROR_SESSION_TOKEN_INVALID", "session_token seems invalid", 401, docMessage: false);
        }
        await access.AttachAsync(http, _session, _ct);
    }

    private void CheckAppToken(GlpiApiClientCheck client)
    {
        if (!client.IsAllowed)
        {
            throw new LegacyApiException(client.ErrorCode!, client.ErrorMessage!);
        }
        _clientId = client.ClientId;
    }

    private async Task<GlpiDataContext> DataAsync() => _data ??= await access.CreateDataContextAsync(http, _session!, _ct);

    // --- Session ------------------------------------------------------------------------------

    /// <summary>API::initSession.</summary>
    private async Task<JsonObject> InitSessionAsync(GlpiApiClientCheck client, ApiSettings apiSettings)
    {
        CheckAppToken(client);
        await access.LogEndpointAsync(http, _clientId, "initSession", null, _ct);

        string login = Str("login") ?? string.Empty;
        string password = Str("password") ?? string.Empty;
        string token = Str("user_token") ?? string.Empty;
        bool credentialsEnabled = apiSettings.EnableLoginWithCredentials;
        bool tokenEnabled = apiSettings.EnableLoginWithExternalToken;

        if ((login.Length > 0 || password.Length > 0) && !credentialsEnabled)
        {
            throw new LegacyApiException("ERROR_LOGIN_WITH_CREDENTIALS_DISABLED", "usage of initSession resource with credentials is disabled", docMessage: false);
        }
        if (token.Length > 0 && !tokenEnabled)
        {
            throw new LegacyApiException("ERROR_LOGIN_WITH_TOKEN_DISABLED", "usage of initSession resource with user token is disabled", docMessage: false);
        }
        if ((!credentialsEnabled || login.Length == 0 || password.Length == 0) && (!tokenEnabled || token.Length == 0))
        {
            string message = credentialsEnabled && tokenEnabled
                ? "parameter(s) login, password or user_token are missing"
                : credentialsEnabled ? "parameter(s) login, password are missing" : "parameter user_token is missing";
            throw new LegacyApiException("ERROR_LOGIN_PARAMETERS_MISSING", message);
        }

        GlpiUser? user = null;
        string errorCode = string.Empty;
        string errorMessage = string.Empty;
        if (tokenEnabled && token.Length > 0)
        {
            user = await access.FindByUserTokenAsync(token, _ct);
            errorCode = "ERROR_GLPI_LOGIN_USER_TOKEN";
            errorMessage = "parameter user_token seems invalid";
        }
        if (user is null && credentialsEnabled && login.Length > 0 && password.Length > 0)
        {
            user = await access.AuthenticateAsync(login, password, _ct);
            errorCode = "ERROR_GLPI_LOGIN";
            errorMessage = "Incorrect username or password";
        }
        if (user is null)
        {
            throw new LegacyApiException(errorCode, errorMessage, 401, docMessage: false);
        }

        GlpiApiSession session = await access.OpenSessionAsync(user, _clientId, GlpiApiSessionStore.NewToken(), _ct);
        sessions.Add(session);
        _session = session;

        JsonObject data = new() { ["session_token"] = session.Token };
        if (Bool("get_full_session"))
        {
            await access.AttachAsync(http, session, _ct);
            data["session"] = await FullSessionAsync();
        }
        return data;
    }

    /// <summary>API::changeActiveEntities (Session::changeActiveEntities).</summary>
    private async Task<JsonNode> ChangeActiveEntitiesAsync()
    {
        bool recursive = false;
        if (_params.ContainsKey("is_recursive"))
        {
            if (_params["is_recursive"] is not JsonValue r || r.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new LegacyApiException("ERROR", "Bad Request");
            }
            recursive = r.GetValue<bool>();
        }

        GlpiApiSession session = _session!;
        if (!_params.ContainsKey("entities_id") || Str("entities_id") == "all")
        {
            session.ActiveEntityId = null;
            session.ActiveEntityRecursive = false;
            return JsonValue.Create(true);
        }

        GlpiDataContext data = await DataAsync();
        int entityId = data.FromApiEntity(Long("entities_id"));
        bool allowed = session.IsAdmin
            || session.ActiveProfileHabilitations.Any(h => h.EntityId == entityId || (h.IsRecursive && data.EntityTree.IsSelfOrDescendant(h.EntityId, entityId)));
        if (!allowed)
        {
            return JsonValue.Create(false);
        }
        session.ActiveEntityId = entityId;
        session.ActiveEntityRecursive = recursive;
        return JsonValue.Create(true);
    }

    /// <summary>API::getMyEntities.</summary>
    private async Task<JsonObject> GetMyEntitiesAsync()
    {
        GlpiDataContext data = await DataAsync();
        bool recursive = Bool("is_recursive");
        Dictionary<int, string> names = await data.EntityTree.GetNamesAsync(_ct);
        JsonArray list = [];
        foreach (GlpiApiHabilitation h in ProfileEntities())
        {
            if (h.IsRecursive && recursive)
            {
                foreach (int son in data.EntityTree.GetSelfAndDescendants(h.EntityId).Where(s => s != h.EntityId))
                {
                    list.Add(new JsonObject { ["id"] = data.ToApiEntity(son), ["name"] = await EntityNameAsync(data, son) });
                }
            }
            list.Add(new JsonObject { ["id"] = data.ToApiEntity(h.EntityId), ["name"] = await EntityNameAsync(data, h.EntityId) });
        }
        _ = names;
        return new JsonObject { ["myentities"] = list };
    }

    private IEnumerable<GlpiApiHabilitation> ProfileEntities()
        => _session!.ActiveProfileHabilitations.DistinctBy(h => h.EntityId);

    private static Task<string> EntityNameAsync(GlpiDataContext data, int entityId)
        => GlpiNames.DropdownNameAsync(data, "glpi_entities", data.ToApiEntity(entityId), CancellationToken.None);

    /// <summary>API::getActiveEntities.</summary>
    private JsonObject GetActiveEntities()
    {
        GlpiApiSession session = _session!;
        int root = access.ScopeFor(session) is var scope && scope.ActiveEntityId is int active ? active : 0;
        GlpiDataContext? data = _data;
        int ToApi(int id) => data?.ToApiEntity(id) ?? (id == (http.RequestServices.GetRequiredService<EntityTreeCache>().GetRootEntityId() ?? 0) ? 0 : id);

        JsonArray actives = [.. access.ActiveEntityIds(session).Select(id => (JsonNode)new JsonObject { ["id"] = ToApi(id) })];
        return new JsonObject
        {
            ["active_entity"] = new JsonObject
            {
                ["id"] = ToApi(session.ActiveEntityId ?? root),
                ["active_entity_recursive"] = session.ActiveEntityId is null || session.ActiveEntityRecursive,
                ["active_entities"] = actives,
            },
        };
    }

    /// <summary>API::changeActiveProfile.</summary>
    private JsonNode ChangeActiveProfile()
    {
        if (!_params.ContainsKey("profiles_id"))
        {
            throw new LegacyApiException("ERROR", "Bad Request");
        }
        int profileId = (int)Long("profiles_id");
        if (_session!.Profiles.Any(p => p.Id == profileId))
        {
            _session.ActiveProfileId = profileId;
            _session.ActiveEntityId = null;
            return JsonValue.Create(true);
        }
        throw new LegacyApiException("ERROR_ITEM_NOT_FOUND", "Item not found", 404, docMessage: false);
    }

    private async Task<JsonObject> ProfileAsync(int profileId, string name)
    {
        GlpiNgDbContext db = (await DataAsync()).Db;
        GlpiProfile? profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId, _ct);
        JsonObject row = GlpiApiProfiles.ProfileRow(profileId, name, profile);
        ProfileRights rights = _session!.IsAdmin || profile is null
            ? ProfileRights.Full
            : new ProfileRights(section => GlpiApiProfiles.LevelOf(profile, section));
        foreach ((string right, JsonNode? value) in GlpiApiProfiles.RightsOf(rights))
        {
            row[right] = value?.DeepClone();
        }
        GlpiDataContext data = await DataAsync();
        JsonArray entities = [];
        foreach (GlpiApiHabilitation h in _session.Habilitations.Where(h => h.ProfileId == profileId))
        {
            entities.Add(new JsonObject
            {
                ["id"] = data.ToApiEntity(h.EntityId),
                ["name"] = h.EntityName,
                ["is_recursive"] = h.IsRecursive ? 1 : 0,
            });
        }
        row["entities"] = entities;
        return row;
    }

    /// <summary>API::getMyProfiles.</summary>
    private async Task<JsonObject> GetMyProfilesAsync()
    {
        JsonArray list = [];
        foreach ((int id, string name) in _session!.Profiles)
        {
            list.Add(await ProfileAsync(id, name));
        }
        return new JsonObject { ["myprofiles"] = list };
    }

    private async Task<JsonObject> ActiveProfileAsync()
    {
        (int id, string name) = _session!.Profiles.FirstOrDefault(p => p.Id == _session.ActiveProfileId);
        return await ProfileAsync(id, name ?? string.Empty);
    }

    /// <summary>Équivalent du $_SESSION de GLPI pour cette session (getFullSession).</summary>
    private async Task<JsonObject> FullSessionAsync()
    {
        GlpiApiSession session = _session!;
        GlpiDataContext data = await DataAsync();
        UserPreferenceValues prefs = await http.RequestServices.GetRequiredService<IUserPreferences>().GetAsync(_ct);
        int[] active = access.ActiveEntityIds(session);
        int activeEntity = session.ActiveEntityId ?? session.ActiveProfileHabilitations.Select(h => h.EntityId).FirstOrDefault(data.RootEntityId);

        JsonObject profiles = [];
        foreach ((int id, string name) in session.Profiles)
        {
            JsonObject entities = [];
            foreach (GlpiApiHabilitation h in session.Habilitations.Where(h => h.ProfileId == id))
            {
                int apiId = data.ToApiEntity(h.EntityId);
                entities[apiId.ToString(CultureInfo.InvariantCulture)] = new JsonObject { ["id"] = apiId, ["name"] = h.EntityName, ["is_recursive"] = h.IsRecursive ? 1 : 0 };
            }
            profiles[id.ToString(CultureInfo.InvariantCulture)] = new JsonObject { ["name"] = name, ["entities"] = entities };
        }

        JsonObject activeEntities = [];
        foreach (int id in active)
        {
            int apiId = data.ToApiEntity(id);
            activeEntities[apiId.ToString(CultureInfo.InvariantCulture)] = apiId;
        }

        List<int> groups = await data.Db.GroupUsers.AsNoTracking().Where(g => g.UserId == session.UserId).Select(g => g.GroupId).ToListAsync(_ct);
        string entityName = await EntityNameAsync(data, activeEntity);

        return new JsonObject
        {
            ["glpi_currenttime"] = GlpiValue.ToText(data.ToUserTime(DateTime.UtcNow)),
            ["glpi_use_mode"] = 0,
            ["glpiID"] = session.UserId,
            ["glpiis_ids_visible"] = prefs.ShowIds ? 1 : 0,
            ["glpifriendlyname"] = session.DisplayName,
            ["glpiname"] = session.UserName,
            ["glpirealname"] = session.LastName,
            ["glpifirstname"] = session.FirstName,
            ["glpidefault_entity"] = data.ToApiEntity(activeEntity),
            ["glpiextauth"] = 0,
            ["glpiauthtype"] = 1,
            ["glpilanguage"] = prefs.Language,
            ["glpilist_limit"] = prefs.ItemsPerPage,
            ["glpi_multientitiesmode"] = await data.Db.Entities.CountAsync(_ct) > 1 ? 1 : 0,
            ["glpiprofiles"] = profiles,
            ["glpiactiveprofile"] = await ActiveProfileAsync(),
            ["glpiactiveentities"] = activeEntities,
            ["glpiactiveentities_string"] = string.Join(",", activeEntities.Select(e => $"'{e.Key}'")),
            ["glpiactive_entity"] = data.ToApiEntity(activeEntity),
            ["glpiactive_entity_recursive"] = session.ActiveEntityId is null || session.ActiveEntityRecursive ? 1 : 0,
            ["glpiactive_entity_name"] = entityName,
            ["glpiactive_entity_shortname"] = entityName.Split(" > ").Last(),
            ["glpigroups"] = new JsonArray([.. groups.Select(g => (JsonNode)g)]),
        };
    }

    /// <summary>Config::getSafeConfig, avec les valeurs propres à cette instance.</summary>
    private async Task<JsonObject> GlpiConfigAsync()
    {
        JsonObject config = GlpiMetadata.Instance.Defaults["safe_config"]!.DeepClone().AsObject();
        UserPreferenceValues prefs = await http.RequestServices.GetRequiredService<IUserPreferences>().GetAsync(_ct);
        config["url_base"] = $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}";
        config["url_base_api"] = _apiUrl;
        config["version"] = GlpiMetadata.Instance.GlpiVersion;
        config["language"] = prefs.Language;
        config["list_limit"] = prefs.ItemsPerPage;
        config["enable_api"] = 1;
        return config;
    }

    // --- Éléments -----------------------------------------------------------------------------

    /// <summary>APIRest::getItemtype : itemtype valide (casse de GLPI), sous-itemtype éventuel en position 2.</summary>
    private GlpiItemtypeInfo? GetItemtype(int index, bool recursive, bool allAssets = false)
    {
        if (_elements.Length > index && _elements[index].Length > 0)
        {
            GlpiItemtypeInfo? info = GlpiMetadata.Instance.FindItemtype(_elements[index]);
            if (info is not null && (info.Table is not null || (allAssets && info.Name == "AllAssets")))
            {
                if (recursive && GetItemtype(index + 2, recursive: false) is GlpiItemtypeInfo sub)
                {
                    _params["parent_itemtype"] = info.Name;
                    return sub;
                }
                return info;
            }
            throw new LegacyApiException("ERROR_RESOURCE_NOT_FOUND_NOR_COMMONDBTM", "resource not found or not an instance of CommonDBTM");
        }
        if (recursive)
        {
            throw new LegacyApiException("ERROR_RESOURCE_MISSING", "missing resource");
        }
        return null;
    }

    /// <summary>APIRest::getId.</summary>
    private int? GetId()
    {
        int? id = _elements.Length > 1 && int.TryParse(_elements[1], out int i) ? i : null;
        int? additional = _elements.Length > 3 && int.TryParse(_elements[3], out int j) ? j : null;
        if (additional is not null || _params.ContainsKey("parent_itemtype"))
        {
            _params["parent_id"] = id;
            id = additional;
        }
        return id;
    }

    private async Task CommonDbtmAsync()
    {
        GlpiItemtypeInfo itemtype = GetItemtype(0, recursive: true)!;
        int? id = GetId();
        Dictionary<string, string> headers = [];
        int code = 200;
        JsonNode? response;

        switch (_verb)
        {
            default:
                if (itemtype.Name == "Document" && id > 0
                    && (http.Request.Headers.Accept.ToString() == "application/octet-stream" || Str("alt") == "media"))
                {
                    await DownloadDocumentAsync(id.Value);
                    return;
                }
                if (id is int itemId)
                {
                    JsonObject item = await GetItemAsync(itemtype, itemId, _params);
                    response = item;
                    if (item["date_mod"]?.ToString() is { Length: > 0 } dateMod && GlpiValue.TryToDateTime(dateMod, out DateTime modified))
                    {
                        headers["Last-Modified"] = modified.ToString("ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) + " GMT";
                    }
                }
                else
                {
                    (JsonArray items, int total, int start, int end) = await GetItemsAsync(itemtype);
                    response = items;
                    if (end > total - 1)
                    {
                        end = total - 1;
                    }
                    if (end - start + 1 < total)
                    {
                        code = 206;
                    }
                    headers["Accept-Range"] = $"{itemtype.Name} {MaxInputVars}";
                    if (total > 0)
                    {
                        headers["Content-Range"] = $"{start}-{end}/{total}";
                    }
                }
                break;

            case "POST":
                response = await CreateItemsAsync(itemtype);
                code = 201;
                if (response is JsonObject created && created["id"] is JsonValue newId)
                {
                    headers["Location"] = $"{_apiUrl}/{itemtype.Name}/{newId}";
                }
                else if (response is JsonArray many)
                {
                    headers["Link"] = string.Join(",", many.OfType<JsonObject>()
                        .Where(r => r["id"] is JsonValue v && v.GetValueKind() == JsonValueKind.Number)
                        .Select(r => $"{_apiUrl}/{itemtype.Name}/{r["id"]}"));
                }
                break;

            case "PUT":
            case "PATCH":
                if (!_params.ContainsKey("input"))
                {
                    throw BadArray();
                }
                if (_params["input"] is JsonObject input && (id > 0 || (id == 0 && itemtype.Name == "Entity")) && !input.ContainsKey("id"))
                {
                    input["id"] = id;
                }
                response = await UpdateItemsAsync(itemtype);
                break;

            case "DELETE":
                if (id is not null)
                {
                    _params["input"] = new JsonObject { ["id"] = id };
                }
                response = await DeleteItemsAsync(itemtype);
                break;
        }

        await Respond(response, code, headers);
    }

    private IGlpiTable TableOf(GlpiItemtypeInfo itemtype) => _data!.Table(itemtype.Table!);

    private async Task<bool> CanViewAsync(GlpiItemtypeInfo itemtype)
    {
        GlpiDataContext data = await DataAsync();
        Type? type = data.Table(itemtype.Table!).ClrType;
        return type is null || data.Rights.CanRead(ProfileSectionMap.ForEntityType(type));
    }

    private static LegacyApiException RightError() => new("ERROR_RIGHT_MISSING", "You don't have permission to perform this action.", 403, docMessage: false);

    private static LegacyApiException NotFound() => new("ERROR_ITEM_NOT_FOUND", "Item not found", 404, docMessage: false);

    private static LegacyApiException BadArray() => new("ERROR_BAD_ARRAY", "input parameter must be an array of objects");

    private LegacyRenderOptions RenderOptions(JsonObject p) => new(
        Truthy(p, "expand_dropdowns", false),
        Truthy(p, "get_hateoas", true),
        p["add_keys_names"] is JsonNode keys ? [.. LegacySearch.AsList(keys).Select(k => k?.ToString() ?? string.Empty)] : []);

    /// <summary>API::getItem.</summary>
    private async Task<JsonObject> GetItemAsync(GlpiItemtypeInfo itemtype, int id, JsonObject p)
    {
        GlpiDataContext data = await DataAsync();
        IGlpiTable table = data.Table(itemtype.Table!);
        Dictionary<int, GlpiRow> rows = await table.GetRowsAsync(data, [id], _ct);
        if (!rows.TryGetValue(id, out GlpiRow? row))
        {
            throw NotFound();
        }
        if (!await CanViewAsync(itemtype))
        {
            throw RightError();
        }

        JsonObject fields = LegacySerializer.Fields(itemtype.Table!, row);

        if (Truthy(p, "with_devices", false))
        {
            JsonObject devices = [];
            foreach (string deviceType in itemtype.Hateoas.Where(h => h.StartsWith("Item_Device", StringComparison.Ordinal)))
            {
                if (GlpiMetadata.Instance.FindItemtype(deviceType)?.Table is not string deviceTable)
                {
                    continue;
                }
                JsonObject found = await RowsAsObjectAsync(deviceTable,
                    GlpiFilter.And(GlpiFilter.Col("items_id", GlpiOp.Eq, id), GlpiFilter.Col("itemtype", GlpiOp.Eq, itemtype.Name), GlpiFilter.Col("is_deleted", GlpiOp.Eq, 0L)),
                    ["items_id", "itemtype", "is_deleted"]);
                if (found.Count > 0)
                {
                    devices[deviceType] = found;
                }
            }
            fields["_devices"] = devices;
        }
        if (Truthy(p, "with_disks", false) && itemtype.Hateoas.Contains("Item_DeviceHardDrive"))
        {
            JsonArray disks = [];
            JsonObject rowsById = await RowsAsObjectAsync("glpi_items_disks",
                GlpiFilter.And(GlpiFilter.Col("items_id", GlpiOp.Eq, id), GlpiFilter.Col("itemtype", GlpiOp.Eq, itemtype.Name), GlpiFilter.Col("is_deleted", GlpiOp.Eq, 0L)),
                ["items_id", "is_deleted"]);
            foreach ((string _, JsonNode? disk) in rowsById)
            {
                JsonObject d = disk!.AsObject();
                d["fsname"] = GlpiValue.TryToLong(GlpiValue.FromJson(d["filesystems_id"]), out long fs) && fs > 0
                    ? await GlpiNames.DropdownNameAsync(data, "glpi_filesystems", (int)fs, _ct) : null;
                disks.Add(new JsonObject { ["name"] = d.DeepClone() });
            }
            fields["_disks"] = disks;
        }
        if (Truthy(p, "with_softwares", false))
        {
            fields["_softwares"] = new JsonArray();
        }
        if (Truthy(p, "with_connections", false))
        {
            fields["_connections"] = new JsonArray();
        }
        if (Truthy(p, "with_networkports", false))
        {
            fields["_networkports"] = await NetworkPortsAsync(itemtype.Name, id);
        }
        if (Truthy(p, "with_infocoms", false))
        {
            fields["_infocoms"] = new JsonArray();
        }
        if (Truthy(p, "with_contracts", false))
        {
            fields["_contracts"] = new JsonArray();
        }
        if (Truthy(p, "with_documents", false))
        {
            fields["_documents"] = await DocumentsOfAsync(itemtype.Name, id);
        }
        foreach (string key in new[] { "with_tickets", "with_problems", "with_changes" })
        {
            if (Truthy(p, key, false))
            {
                fields["_" + key[5..]] = new JsonArray();
            }
        }
        if (Truthy(p, "with_notes", false))
        {
            JsonObject notes = await RowsAsObjectAsync("glpi_notepads",
                GlpiFilter.And(GlpiFilter.Col("itemtype", GlpiOp.Eq, itemtype.Name), GlpiFilter.Col("items_id", GlpiOp.Eq, id)), []);
            fields["_notes"] = new JsonArray([.. notes.Select(n => n.Value?.DeepClone())]);
        }
        if (Truthy(p, "with_logs", false))
        {
            fields["_logs"] = await RowsAsObjectAsync("glpi_logs",
                GlpiFilter.And(GlpiFilter.Col("itemtype", GlpiOp.Eq, itemtype.Name), GlpiFilter.Col("items_id", GlpiOp.Eq, id)), []);
        }

        LegacyRenderOptions options = RenderOptions(p);
        JsonObject? keysNames = null;
        if (options.AddKeysNames.Count > 0)
        {
            keysNames = [];
            foreach (string key in options.AddKeysNames)
            {
                if (fields[key] is JsonValue v && v.TryGetValue(out long refId))
                {
                    string refTable = key == "id" ? itemtype.Table! : GlpiNames.TableForForeignKey(key);
                    keysNames[key] = await GlpiNames.DropdownNameAsync(data, refTable, (int)refId, _ct);
                }
            }
        }
        await LegacySerializer.ParseDropdownsAsync(data, _apiUrl, fields, options, _ct);
        if (options.GetHateoas)
        {
            JsonArray links = fields["links"] as JsonArray ?? [];
            foreach (string hclass in itemtype.Hateoas)
            {
                links.Add(new JsonObject { ["rel"] = hclass, ["href"] = $"{_apiUrl}/{itemtype.Name}/{id}/{hclass}/" });
            }
            if (links.Count > 0)
            {
                fields["links"] = links;
            }
        }
        if (keysNames is not null)
        {
            fields["_keys_names"] = keysNames;
        }
        if (Truthy(p, "get_sha1", false))
        {
            string json = fields.ToJsonString(Json);
            return new JsonObject { ["sha1"] = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(json))) };
        }
        return fields;
    }

    /// <summary>getAllDataFromTable : lignes indexées par identifiant, colonnes GLPI.</summary>
    private async Task<JsonObject> RowsAsObjectAsync(string table, GlpiFilter filter, string[] unset)
    {
        GlpiDataContext data = await DataAsync();
        IGlpiTable t = data.Table(table);
        GlpiPage page = await t.QueryAsync(data, filter, null, 0, null, _ct);
        Dictionary<int, GlpiRow> rows = await t.GetRowsAsync(data, page.Ids, _ct);
        JsonObject result = [];
        foreach (int rowId in page.Ids)
        {
            if (rows.TryGetValue(rowId, out GlpiRow? row))
            {
                JsonObject fields = LegacySerializer.Fields(table, row);
                foreach (string u in unset)
                {
                    fields.Remove(u);
                }
                result[rowId.ToString(CultureInfo.InvariantCulture)] = fields;
            }
        }
        return result;
    }

    /// <summary>API::getNetworkPorts, simplifié : ports de l'élément regroupés par type d'instanciation.</summary>
    private async Task<JsonObject> NetworkPortsAsync(string itemtype, int id)
    {
        JsonObject ports = await RowsAsObjectAsync("glpi_networkports",
            GlpiFilter.And(GlpiFilter.Col("items_id", GlpiOp.Eq, id), GlpiFilter.Col("itemtype", GlpiOp.Eq, itemtype), GlpiFilter.Col("is_deleted", GlpiOp.Eq, 0L)), []);
        JsonObject result = [];
        foreach ((string portId, JsonNode? port) in ports)
        {
            JsonObject p = port!.AsObject();
            string type = p["instantiation_type"]?.ToString() ?? "NetworkPortEthernet";
            p["netport_id"] = int.Parse(portId, CultureInfo.InvariantCulture);
            if (result[type] is not JsonArray list)
            {
                list = [];
                result[type] = list;
            }
            list.Add(p.DeepClone());
        }
        return result;
    }

    private async Task<JsonArray> DocumentsOfAsync(string itemtype, int id)
    {
        GlpiDataContext data = await DataAsync();
        IGlpiTable links = data.Table("glpi_documents_items");
        GlpiPage page = await links.QueryAsync(data,
            GlpiFilter.And(GlpiFilter.Col("itemtype", GlpiOp.Eq, itemtype), GlpiFilter.Col("items_id", GlpiOp.Eq, id)), null, 0, null, _ct);
        Dictionary<int, GlpiRow> linkRows = await links.GetRowsAsync(data, page.Ids, _ct);
        Dictionary<int, GlpiRow> docs = await data.Table("glpi_documents").GetRowsAsync(data,
            [.. linkRows.Values.Select(r => (int)r.GetValueOrDefault("documents_id").ToLong())], _ct);
        JsonArray result = [];
        foreach (GlpiRow link in linkRows.Values)
        {
            if (!docs.TryGetValue((int)link.GetValueOrDefault("documents_id").ToLong(), out GlpiRow? doc))
            {
                continue;
            }
            JsonObject entry = new()
            {
                ["assocID"] = link.Id,
                ["assocdate"] = GlpiValue.ToText(link.GetValueOrDefault("date_creation")),
                ["entityID"] = (long?)doc.GetValueOrDefault("entities_id").ToLong(),
                ["entity"] = await GlpiNames.DropdownNameAsync(data, "glpi_entities", (int)doc.GetValueOrDefault("entities_id").ToLong(), _ct),
                ["headings"] = doc.GetValueOrDefault("documentcategories_id").ToLong() is long cat and > 0
                    ? await GlpiNames.DropdownNameAsync(data, "glpi_documentcategories", (int)cat, _ct) : null,
            };
            foreach ((string key, JsonNode? value) in LegacySerializer.Fields("glpi_documents", doc))
            {
                entry[key] = value?.DeepClone();
            }
            result.Add(entry);
        }
        return result;
    }

    /// <summary>API::getItems.</summary>
    private async Task<(JsonArray Items, int Total, int Start, int End)> GetItemsAsync(GlpiItemtypeInfo itemtype)
    {
        GlpiDataContext data = await DataAsync();
        UserPreferenceValues prefs = await http.RequestServices.GetRequiredService<IUserPreferences>().GetAsync(_ct);
        (int start, int end) = LegacySearch.ParseRange(_params["range"], prefs.ItemsPerPage);
        string order = (Str("order") ?? "ASC").ToUpperInvariant();
        if (order is not ("ASC" or "DESC"))
        {
            throw new LegacyApiException("ERROR", "order must be DESC or ASC");
        }
        string table = itemtype.Table!;
        string sort = Str("sort") ?? "id";
        IReadOnlyList<GlpiColumn> columns = GlpiMetadata.Instance.ColumnsOf(table);
        if (columns.All(c => c.Name != sort))
        {
            throw new LegacyApiException("ERROR", $"sort param is not a field of {table}");
        }
        if (!await CanViewAsync(itemtype))
        {
            throw RightError();
        }

        List<GlpiFilter> filters = [];
        if (itemtype.MaybeDeleted)
        {
            filters.Add(GlpiFilter.Col("is_deleted", GlpiOp.Eq, Bool("is_deleted") ? 1L : 0L));
        }
        filters.Add(await ParentFilterAsync(itemtype));

        // searchText
        if (_params["searchText"] is JsonObject searchText)
        {
            Dictionary<string, string> texts = searchText.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty);
            if (texts.Keys.SequenceEqual(["all"]))
            {
                string value = texts["all"];
                texts.Clear();
                texts[itemtype.Name.StartsWith("Device", StringComparison.Ordinal) ? "designation"
                    : itemtype.Name.StartsWith("Item_Device", StringComparison.Ordinal) ? "itemtype" : "name"] = value;
                if (columns.Any(c => c.Name == "comment"))
                {
                    texts["comment"] = value;
                }
            }
            foreach ((string field, string value) in texts)
            {
                if (LegacySerializer.IsUndisclosed(table, field))
                {
                    continue;
                }
                if (columns.All(c => c.Name != field))
                {
                    throw new LegacyApiException("ERROR_FIELD_NOT_FOUND", $"Field {field} is not valid for {itemtype.Name} item.");
                }
                if (value.Length > 0)
                {
                    filters.Add(GlpiFilter.Col(field, GlpiOp.Like, GlpiValue.MakeTextSearchPattern(value)));
                }
            }
        }

        IGlpiTable t = data.Table(table);
        GlpiPage page = await t.QueryAsync(data, GlpiFilter.And([.. filters]), [new GlpiSort(sort, order == "DESC")], start, end - start + 1, _ct);
        if (start > page.Total)
        {
            throw new LegacyApiException("ERROR_RANGE_EXCEED_TOTAL", "Provided range exceed total count of data: " + page.Total);
        }

        Dictionary<int, GlpiRow> rows = await t.GetRowsAsync(data, page.Ids, _ct);
        LegacyRenderOptions options = RenderOptions(_params);
        bool onlyId = Truthy(_params, "only_id", false);
        JsonArray items = [];
        foreach (int id in page.Ids)
        {
            if (!rows.TryGetValue(id, out GlpiRow? row))
            {
                continue;
            }
            JsonObject fields;
            if (onlyId)
            {
                fields = new JsonObject { ["id"] = id };
                await LegacySerializer.ParseDropdownsAsync(data, _apiUrl, fields, options, _ct);
                if (options.GetHateoas)
                {
                    AddChildLinks(fields, itemtype, id);
                }
            }
            else
            {
                fields = await LegacySerializer.RenderAsync(data, _apiUrl, itemtype.Name, table, row, options, _ct);
            }
            if (Truthy(_params, "with_networkports", false))
            {
                fields["_networkports"] = await NetworkPortsAsync(itemtype.Name, id);
            }
            items.Add(fields);
        }
        return (items, page.Total, start, end);
    }

    private void AddChildLinks(JsonObject fields, GlpiItemtypeInfo itemtype, int id)
    {
        JsonArray links = fields["links"] as JsonArray ?? [];
        foreach (string hclass in itemtype.Hateoas)
        {
            links.Add(new JsonObject { ["rel"] = hclass, ["href"] = $"{_apiUrl}/{itemtype.Name}/{id}/{hclass}/" });
        }
        if (links.Count > 0)
        {
            fields["links"] = links;
        }
    }

    /// <summary>Filtre « sous-éléments » de getItems (/:itemtype/:id/:sub_itemtype).</summary>
    private async Task<GlpiFilter> ParentFilterAsync(GlpiItemtypeInfo itemtype)
    {
        if (Str("parent_itemtype") is not string parentName || _params["parent_id"] is null)
        {
            return GlpiFilter.True;
        }
        GlpiItemtypeInfo parent = GlpiMetadata.Instance.FindItemtype(parentName)
            ?? throw new LegacyApiException("ERROR_ITEMTYPE_NOT_FOUND_NOR_COMMONDBTM", "parent itemtype not found or not an instance of CommonDBTM");
        int parentId = (int)Long("parent_id");
        GlpiDataContext data = await DataAsync();
        Dictionary<int, GlpiRow> parentRows = await data.Table(parent.Table!).GetRowsAsync(data, [parentId], _ct);
        if (!parentRows.TryGetValue(parentId, out GlpiRow? parentRow))
        {
            throw NotFound();
        }
        if (!await CanViewAsync(parent))
        {
            throw RightError();
        }

        HashSet<string> childColumns = [.. GlpiMetadata.Instance.ColumnsOf(itemtype.Table!).Select(c => c.Name)];
        HashSet<string> parentColumns = [.. GlpiMetadata.Instance.ColumnsOf(parent.Table!).Select(c => c.Name)];
        string fkParent = parent.ForeignKey ?? GlpiSearchPath.ForeignKeyForTable(parent.Table!);
        string fkChild = itemtype.ForeignKey ?? GlpiSearchPath.ForeignKeyForTable(itemtype.Table!);

        if (childColumns.Contains(fkParent))
        {
            return GlpiFilter.Col(fkParent, GlpiOp.Eq, parentId);
        }
        if (childColumns.Contains("itemtype") && childColumns.Contains("items_id"))
        {
            return GlpiFilter.And(GlpiFilter.Col("itemtype", GlpiOp.Eq, parent.Name), GlpiFilter.Col("items_id", GlpiOp.Eq, parentId));
        }
        if (parentColumns.Contains(fkChild))
        {
            return GlpiFilter.Col("id", GlpiOp.Eq, parentRow.GetValueOrDefault(fkChild) ?? GlpiDefaults.DefaultValue(parent.Table!, fkChild));
        }
        if (parentColumns.Contains("itemtype") && parentColumns.Contains("items_id"))
        {
            return GlpiValue.ToText(parentRow.GetValueOrDefault("itemtype")) == itemtype.Name
                ? GlpiFilter.Col("id", GlpiOp.Eq, parentRow.GetValueOrDefault("items_id"))
                : GlpiFilter.False;
        }
        return GlpiFilter.True;
    }

    /// <summary>API::getMultipleItems.</summary>
    private async Task<JsonArray> GetMultipleItemsAsync()
    {
        if (_params["items"] is not JsonNode items || items is JsonValue)
        {
            throw BadArray();
        }
        JsonArray result = [];
        foreach (JsonNode? node in LegacySearch.AsList(items))
        {
            if (node is not JsonObject item || (item["items_id"] is null && item["itemtype"] is null))
            {
                throw BadArray();
            }
            GlpiItemtypeInfo info = GlpiMetadata.Instance.FindItemtype(item["itemtype"]?.ToString())
                ?? throw NotFound();
            if (info.Table is null)
            {
                throw NotFound();
            }
            result.Add(await GetItemAsync(info, (int)GlpiValue.FromJson(item["items_id"]).ToLong(), _params));
        }
        return result;
    }

    /// <summary>APIRest « search » : API::searchItems et ses en-têtes de pagination.</summary>
    private async Task SearchAsync()
    {
        GlpiItemtypeInfo itemtype = GetItemtype(1, recursive: true, allAssets: true)!;
        GlpiDataContext data = await DataAsync();
        if (itemtype.Table is not null && !await CanViewAsync(itemtype))
        {
            throw RightError();
        }
        UserPreferenceValues prefs = await http.RequestServices.GetRequiredService<IUserPreferences>().GetAsync(_ct);
        bool multiEntities = await data.Db.Entities.CountAsync(_ct) > 1;
        LegacySearch search = new(data, prefs.ItemsPerPage, multiEntities);
        (JsonObject body, int total, int count, string range) = await search.SearchAsync(itemtype, _params, _ct);

        Dictionary<string, string> headers = new() { ["Accept-Range"] = $"{itemtype.Name} {MaxInputVars}" };
        if (total > 0)
        {
            headers["Content-Range"] = range;
        }
        await Respond(body, count >= total ? 200 : 206, headers);
    }

    // --- Écritures ------------------------------------------------------------------------------

    private static (JsonArray Items, bool IsMultiple) InputList(JsonNode? input) => input switch
    {
        JsonObject o when o.All(kv => int.TryParse(kv.Key, out _)) && o.Count > 0 && o.All(kv => kv.Value is JsonObject)
            => (LegacySearch.AsList(o), true),
        JsonObject o => ([o.DeepClone()], false),
        JsonArray a => (a, true),
        _ => ([], true),
    };

    /// <summary>API::createItems.</summary>
    private async Task<JsonNode> CreateItemsAsync(GlpiItemtypeInfo itemtype)
    {
        (JsonArray inputs, bool multiple) = InputList(_params["input"]);
        if (_params["input"] is null or JsonValue)
        {
            throw BadArray();
        }

        GlpiDataContext data = await DataAsync();
        IGlpiTable table = data.Table(itemtype.Table!);
        JsonArray results = [];
        int failed = 0;
        int index = 0;
        foreach (JsonNode? node in inputs)
        {
            JsonObject result;
            if (node is not JsonObject obj)
            {
                failed++;
                result = new JsonObject { ["id"] = false, ["message"] = "input parameter must be an array of objects" };
            }
            else
            {
                try
                {
                    int id;
                    if (itemtype.Name == "Document" && http.Request.HasFormContentType && http.Request.Form.Files.Count > index)
                    {
                        id = await UploadDocumentAsync(http.Request.Form.Files[index], obj);
                    }
                    else
                    {
                        id = await table.InsertAsync(data, new GlpiInput(obj.ToDictionary(kv => kv.Key, kv => kv.Value?.DeepClone())), _ct);
                    }
                    result = new JsonObject { ["id"] = id, ["message"] = string.Empty };
                }
                catch (GlpiWriteException e)
                {
                    failed++;
                    result = new JsonObject { ["id"] = false, ["message"] = e.Message };
                }
            }
            results.Add(result);
            index++;
        }

        if (multiple)
        {
            if (failed == inputs.Count && inputs.Count > 0)
            {
                throw new LegacyApiException("ERROR_GLPI_ADD", string.Empty, 400, docMessage: false) { Payload = results };
            }
            if (failed > 0)
            {
                throw new LegacyApiException("ERROR_GLPI_PARTIAL_ADD", string.Empty, 207, docMessage: false) { Payload = results };
            }
            return results;
        }
        if (failed > 0)
        {
            throw new LegacyApiException("ERROR_GLPI_ADD", results[0]!["message"]!.ToString(), 400, docMessage: false);
        }
        return results[0]!.DeepClone();
    }

    /// <summary>API::updateItems.</summary>
    private async Task<JsonNode> UpdateItemsAsync(GlpiItemtypeInfo itemtype)
    {
        (JsonArray inputs, bool multiple) = InputList(_params["input"]);
        if (_params["input"] is null or JsonValue)
        {
            throw BadArray();
        }

        GlpiDataContext data = await DataAsync();
        IGlpiTable table = data.Table(itemtype.Table!);
        JsonArray results = [];
        int failed = 0;
        foreach (JsonObject obj in inputs.OfType<JsonObject>())
        {
            if (obj["id"] is null)
            {
                continue;
            }
            int id = (int)GlpiValue.FromJson(obj["id"]).ToLong();
            string key = id.ToString(CultureInfo.InvariantCulture);
            if ((await table.GetRowsAsync(data, [id], _ct)).Count == 0)
            {
                failed++;
                // GLPI passe à l'élément suivant sans consigner le résultat de celui-ci.
                continue;
            }
            GlpiInput input = new(obj.ToDictionary(kv => kv.Key, kv => kv.Value?.DeepClone()));
            if (Str("parent_itemtype") is string parent && GlpiNames.ForeignKeyForItemtype(parent) is string fk && !input.ContainsKey(fk))
            {
                input[fk] = _params["parent_id"]?.DeepClone();
            }
            try
            {
                await table.UpdateAsync(data, id, input, _ct);
                results.Add(new JsonObject { [key] = true, ["message"] = string.Empty });
            }
            catch (GlpiWriteException e)
            {
                failed++;
                results.Add(new JsonObject { [key] = false, ["message"] = e.Message });
            }
        }

        if (multiple)
        {
            if (failed == inputs.Count && inputs.Count > 0)
            {
                throw new LegacyApiException("ERROR_GLPI_UPDATE", string.Empty, 400, docMessage: false) { Payload = results };
            }
            if (failed > 0)
            {
                throw new LegacyApiException("ERROR_GLPI_PARTIAL_UPDATE", string.Empty, 207, docMessage: false) { Payload = results };
            }
            return results;
        }
        if (failed > 0)
        {
            throw new LegacyApiException("ERROR_GLPI_UPDATE", results.Count > 0 ? results[0]!["message"]!.ToString() : "Item not found", 400, docMessage: false);
        }
        return results;
    }

    /// <summary>API::deleteItems.</summary>
    private async Task<JsonNode> DeleteItemsAsync(GlpiItemtypeInfo itemtype)
    {
        (JsonArray inputs, bool multiple) = InputList(_params["input"]);
        if (_params["input"] is null or JsonValue)
        {
            throw BadArray();
        }
        bool forcePurge = Truthy(_params, "force_purge", false);

        GlpiDataContext data = await DataAsync();
        IGlpiTable table = data.Table(itemtype.Table!);
        JsonArray results = [];
        int failed = 0;
        foreach (JsonObject obj in inputs.OfType<JsonObject>())
        {
            if (obj["id"] is null)
            {
                continue;
            }
            int id = (int)GlpiValue.FromJson(obj["id"]).ToLong();
            string key = id.ToString(CultureInfo.InvariantCulture);
            try
            {
                await table.DeleteAsync(data, id, forcePurge || !table.HasTrash, _ct);
                results.Add(new JsonObject { [key] = true, ["message"] = string.Empty });
            }
            catch (GlpiWriteException e)
            {
                failed++;
                results.Add(new JsonObject { [key] = false, ["message"] = e.Message });
            }
        }

        if (multiple)
        {
            if (failed == inputs.Count && inputs.Count > 0)
            {
                throw new LegacyApiException("ERROR_GLPI_DELETE", string.Empty, 400, docMessage: false) { Payload = results };
            }
            if (failed > 0)
            {
                throw new LegacyApiException("ERROR_GLPI_PARTIAL_DELETE", string.Empty, 207, docMessage: false) { Payload = results };
            }
            return results;
        }
        if (failed > 0)
        {
            throw new LegacyApiException("ERROR_GLPI_DELETE", results[0]!["message"]!.ToString(), 400, docMessage: false);
        }
        return results;
    }

    // --- Documents ------------------------------------------------------------------------------

    private async Task<int> UploadDocumentAsync(IFormFile file, JsonObject input)
    {
        GlpiDataContext data = await DataAsync();
        DocumentService documents = http.RequestServices.GetRequiredService<DocumentService>();
        await using Stream content = file.OpenReadStream();
        int? category = GlpiValue.TryToLong(GlpiValue.FromJson(input["documentcategories_id"]), out long c) && c > 0 ? (int)c : null;
        int id = await documents.UploadAsync(file.FileName, content, file.ContentType, data.UserDisplayName, data.UserId, category, _ct);

        GlpiInput rest = new(input.Where(kv => kv.Key is not ("_filename" or "_prefix_filename")).ToDictionary(kv => kv.Key, kv => kv.Value?.DeepClone()));
        if (rest.Count > 0)
        {
            await data.Table("glpi_documents").UpdateAsync(data, id, rest, _ct);
        }
        if (GlpiValue.ToText(GlpiValue.FromJson(input["itemtype"])) is { Length: > 0 } itemtype
            && GlpiValue.TryToLong(GlpiValue.FromJson(input["items_id"]), out long itemId) && itemId > 0)
        {
            await documents.AttachAsync(itemtype, (int)itemId, id, _ct);
        }
        return id;
    }

    private async Task DownloadDocumentAsync(int id)
    {
        GlpiDataContext data = await DataAsync();
        if ((await data.Table("glpi_documents").GetRowsAsync(data, [id], _ct)).Count == 0)
        {
            throw NotFound();
        }
        if (!await CanViewAsync(GlpiMetadata.Instance.FindItemtype("Document")!))
        {
            throw RightError();
        }
        DocumentService documents = http.RequestServices.GetRequiredService<DocumentService>();
        Document? document = await documents.GetAsync(id, _ct);
        Stream? content = document is null ? null : documents.OpenContent(document);
        if (document is null || content is null)
        {
            throw NotFound();
        }
        await using (content)
        {
            http.Response.ContentType = "application/octet-stream";
            http.Response.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileNameStar = document.FileName,
            }.ToString();
            await content.CopyToAsync(http.Response.Body, _ct);
        }
    }

    // --- Actions massives -----------------------------------------------------------------------

    private JsonArray GetMassiveActions(GlpiItemtypeInfo itemtype, string? id, bool isDeleted)
    {
        JsonObject labels = GlpiMetadata.Instance.Defaults["massive_actions"]!.AsObject();
        IGlpiTable table = http.RequestServices.GetRequiredService<GlpiTableCatalog>().Get(itemtype.Table!);
        List<string> keys = isDeleted
            ? ["MassiveAction:purge", "MassiveAction:restore"]
            : ["MassiveAction:update", table.HasTrash ? "MassiveAction:delete" : "MassiveAction:purge"];
        if (!isDeleted && table.IsMapped("comment"))
        {
            keys.Add("MassiveAction:amend_comment");
        }
        _ = id;
        return [.. keys.Select(k => (JsonNode)new JsonObject { ["key"] = k, ["label"] = labels[k]?.ToString() ?? k })];
    }

    private static JsonArray GetMassiveActionParameters(string? key) => key switch
    {
        "MassiveAction:amend_comment" => [new JsonObject { ["name"] = "amendment", ["type"] = "text" }],
        "MassiveAction:add_note" => [new JsonObject { ["name"] = "add_note", ["type"] = "text" }],
        null => throw new LegacyApiException("ERROR_MASSIVEACTION_KEY", "Missing or invalid massive action key."),
        _ => [],
    };

    private async Task ApplyMassiveActionAsync()
    {
        JsonArray ids = LegacySearch.AsList(_params["ids"]);
        if (ids.Count == 0)
        {
            throw new LegacyApiException("ERROR_MASSIVEACTION_NO_IDS", "No ids supplied");
        }
        GlpiItemtypeInfo itemtype = GetItemtype(1, recursive: false)!;
        string key = Element(2) ?? throw new LegacyApiException("ERROR_MASSIVEACTION_KEY", "Missing or invalid massive action key.");
        JsonObject input = _params["input"] as JsonObject ?? [];

        GlpiDataContext data = await DataAsync();
        IGlpiTable table = data.Table(itemtype.Table!);
        int ok = 0, ko = 0, noright = 0;
        JsonArray messages = [];
        foreach (int id in ids.Select(i => (int)GlpiValue.FromJson(i).ToLong()))
        {
            try
            {
                switch (key)
                {
                    case "MassiveAction:delete":
                        await table.DeleteAsync(data, id, !table.HasTrash, _ct);
                        break;
                    case "MassiveAction:purge":
                        await table.DeleteAsync(data, id, true, _ct);
                        break;
                    case "MassiveAction:restore":
                        await table.RestoreAsync(data, id, _ct);
                        break;
                    case "MassiveAction:update":
                        await table.UpdateAsync(data, id, new GlpiInput(input.ToDictionary(kv => kv.Key, kv => kv.Value?.DeepClone())), _ct);
                        break;
                    case "MassiveAction:amend_comment":
                    {
                        Dictionary<int, GlpiRow> rows = await table.GetRowsAsync(data, [id], _ct);
                        string current = rows.TryGetValue(id, out GlpiRow? row) ? GlpiValue.ToText(row.GetValueOrDefault("comment")) ?? string.Empty : string.Empty;
                        string amendment = input["amendment"]?.ToString() ?? string.Empty;
                        await table.UpdateAsync(data, id, new GlpiInput { ["comment"] = current.Length == 0 ? amendment : current + "\n\n" + amendment }, _ct);
                        break;
                    }
                    default:
                        throw new LegacyApiException("ERROR_MASSIVEACTION_KEY", "Missing or invalid massive action key.");
                }
                ok++;
            }
            catch (GlpiWriteException e) when (e.IsRightError)
            {
                noright++;
            }
            catch (GlpiWriteException e)
            {
                ko++;
                messages.Add(e.Message);
            }
        }

        JsonObject body = new() { ["ok"] = ok, ["ko"] = ko, ["noright"] = noright, ["messages"] = messages };
        int code = ok == ids.Count ? 200 : ok > 0 ? 207 : 422;
        await Respond(body, code);
    }

    // --- Réponses -------------------------------------------------------------------------------

    private async Task InlineDocumentationAsync()
    {
        string markdown = GlpiMetadata.Instance.ApiRestDocumentation;
        if ((http.Request.ContentType ?? string.Empty).Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            http.Response.ContentType = "application/json; charset=UTF-8";
            await http.Response.WriteAsync(markdown, _ct);
            return;
        }
        http.Response.ContentType = "text/html; charset=UTF-8";
        string html = Modules.KnowledgeBase.Services.MarkdownRenderer.ToHtml(markdown);
        await http.Response.WriteAsync(
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Rest API</title></head><body><div id='page' class='documentation'>"
            + html + "</div></body></html>", _ct);
    }

    private async Task Respond(JsonNode? response, int code = 200, Dictionary<string, string>? headers = null)
    {
        foreach ((string key, string value) in headers ?? [])
        {
            http.Response.Headers[key] = value;
        }
        http.Response.StatusCode = code;
        http.Response.ContentType = "application/json; charset=UTF-8";
        http.Response.Headers.Expires = "Tue, 1 Jul 2003 05:00:00 GMT";
        http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        http.Response.Headers.Pragma = "no-cache";
        await http.Response.WriteAsync(response is null ? string.Empty : response.ToJsonString(Json), _ct);
    }

    /// <summary>API::returnError : [code, message], suivi d'un renvoi vers la documentation si demandé.</summary>
    private async Task ReturnError(LegacyApiException e)
    {
        if (http.Response.HasStarted)
        {
            return;
        }
        JsonNode message = e.Payload?.DeepClone() ?? JsonValue.Create(e.DocMessage
            ? $"{e.Message}; view documentation in your browser at {_apiUrl}/#{e.StatusCode}"
            : e.Message)!;
        await Respond(new JsonArray(e.StatusCode, message), e.HttpCode);
    }

    // --- Lecture des paramètres -----------------------------------------------------------------

    private string? Str(string key) => _params[key] is JsonNode n ? GlpiValue.ToText(GlpiValue.FromJson(n)) : null;

    private long Long(string key) => GlpiValue.FromJson(_params[key]).ToLong();

    private bool Bool(string key) => Truthy(_params, key, false);

    private string? Element(int index) => _elements.Length > index && _elements[index].Length > 0 ? _elements[index] : null;

    private static bool Truthy(JsonObject p, string key, bool defaultValue)
        => p[key] is JsonNode n ? GlpiValue.TryToBool(GlpiValue.FromJson(n), out bool b) ? b : true : defaultValue;
}
