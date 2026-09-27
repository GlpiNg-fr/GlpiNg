using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GlpiNg.Web.Api.Glpi;
using GlpiNg.Web.Api.Glpi.Data;
using GlpiNg.Web.Api.Legacy;
using GlpiNg.Web.Controllers;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Api.HighLevel;

/// <summary>Route de l'API v2 : un chemin de la documentation OpenAPI de GLPI, compilé.</summary>
public sealed record HlRoute(string Template, Regex Pattern, IReadOnlyList<string> Parameters, JsonObject Operations);

/// <summary>
/// API « de haut niveau » de GLPI (api.php, v2.0 à v2.3) : mêmes chemins, mêmes schémas, mêmes
/// paramètres (filter RSQL, start, limit, sort), mêmes en-têtes (GLPI-Entity, GLPI-Profile,
/// Content-Range) et mêmes erreurs. Les routes sont celles de la documentation OpenAPI générée par
/// GLPI lui-même ; leur traitement est générique (recherche, lecture, création, modification,
/// suppression selon le schéma), à l'exception des routes de session et de service.
/// </summary>
public sealed class HighLevelApiHandler(HttpContext http, GlpiApiAccess access, SettingsCacheService settings)
{
    private static readonly ConcurrentDictionary<string, List<HlRoute>> RoutesByVersion = new();

    private static readonly JsonSerializerOptions Json = LegacyApiHandler.Json;

    private readonly CancellationToken _ct = http.RequestAborted;
    private string _version = string.Empty;
    private GlpiApiSession? _session;
    private GlpiDataContext? _data;
    private IReadOnlyList<string> _scopes = [];
    private JsonObject _params = [];

    public static async Task HandleAsync(HttpContext http, string? path)
    {
        // La barre finale compte : « /Assets/ » (liste des types) n'est pas « /Assets ».
        string relative = "/" + (path ?? string.Empty).TrimStart('/');
        if (Regex.IsMatch(relative, @"^/v1(/|$)"))
        {
            await LegacyApiHandler.HandleAsync(http, relative);
            return;
        }
        HighLevelApiHandler handler = new(http,
            http.RequestServices.GetRequiredService<GlpiApiAccess>(),
            http.RequestServices.GetRequiredService<SettingsCacheService>());
        await handler.HandleAsync(relative);
    }

    private async Task HandleAsync(string relative)
    {
        try
        {
            // Version : préfixe /v2.x/ ou en-tête GLPI-API-Version, à défaut la plus récente.
            string requested = http.Request.Headers["GLPI-API-Version"].ToString();
            Match versionMatch = Regex.Match(relative, @"^/v(\d+(?:\.\d+)*)(/|$)");
            if (versionMatch.Success)
            {
                requested = versionMatch.Groups[1].Value;
                relative = "/" + relative[versionMatch.Length..].TrimStart('/');
            }
            _version = NormalizeVersion(requested);
            if (relative.Length > 1)
            {
                relative = relative.TrimEnd('/') is { Length: > 0 } trimmed && !IsIndexRoute(relative) ? trimmed : relative;
            }

            string method = http.Request.Method.ToUpperInvariant();
            bool head = method == "HEAD";
            if (head)
            {
                method = "GET";
            }

            await ReadParametersAsync(method);

            ApiSettings apiSettings = await settings.ReadSectionAsync<ApiSettings>("ApiSettings", _ct);
            bool enabled = apiSettings.EnableHighLevelApi;

            if (method == "OPTIONS")
            {
                Options();
                return;
            }

            // Routes publiques.
            switch (relative)
            {
                case "/" when method == "GET":
                    await Respond(new JsonObject { ["message"] = "Welcome to GLPI API", ["api_versions"] = ApiVersions() });
                    return;
                case "/doc" when method == "GET":
                    await DocumentationAsync();
                    return;
                case "/doc.json" when method == "GET":
                    string doc = GlpiMetadata.Instance.OpenApiDocument(_version)
                        .Replace("\"http://localhost/api.php\"", JsonSerializer.Serialize(ApiRoot()));
                    await RespondRaw(doc, 200, "application/json");
                    return;
                case "/token" when method == "POST":
                    await TokenAsync();
                    return;
                case "/authorize":
                    throw new HlApiException(400, "ERROR_INVALID_PARAMETER", "Invalid OAuth token",
                        JsonValue.Create("The authorization code grant is not supported by GlpiNg; use the password or client_credentials grant."));
                case "/getting-started" when method == "GET":
                    await DocumentationAsync();
                    return;
                case "/status/all" when method == "GET":
                    await Respond(StatusAll());
                    return;
            }

            if (!enabled)
            {
                throw HlApiException.AccessDenied("The High-Level API is disabled");
            }

            if (!await AuthenticateAsync())
            {
                await Respond(new JsonObject
                {
                    ["title"] = "You are not authenticated",
                    ["detail"] = "The Authorization header is missing or invalid",
                    ["status"] = "ERROR_UNAUTHENTICATED",
                }, 401);
                return;
            }

            // Routes de session et de service.
            switch (relative)
            {
                case "/session" when method == "GET":
                    await Respond(await SessionAsync());
                    return;
                case "/Session/EntityTree" when method == "GET":
                    await Respond(await EntityTreeAsync());
                    return;
                case "/status" when method == "GET":
                    await Respond(new JsonObject { ["all"] = new JsonObject { ["href"] = "/status/all" }, ["db"] = new JsonObject { ["href"] = "/status/db" } });
                    return;
                case "/locales" when method == "GET":
                    await Respond(new JsonObject());
                    return;
                case "/Administration/User/Me" when method == "GET":
                    await OneAsync("User", GlpiFilter.Col("id", GlpiOp.Eq, _session!.UserId));
                    return;
            }
            if (relative.StartsWith("/status/", StringComparison.Ordinal) && method == "GET")
            {
                await Respond(StatusAll()[relative[8..].ToLowerInvariant()]?.DeepClone() ?? new JsonObject { ["status"] = "NO_DATA" });
                return;
            }

            await DispatchAsync(relative, method, head);
        }
        catch (HlApiException e)
        {
            await Respond(Error(e.Status, e.Title, e.Detail), e.HttpCode);
        }
        catch (GlpiWriteException e) when (e.IsRightError)
        {
            await Respond(Error("ERROR_RIGHT_MISSING", "You don't have permission to perform this action."), 403);
        }
    }

    // --- Paramètres et version ----------------------------------------------------------------

    private static string NormalizeVersion(string requested)
    {
        IReadOnlyList<string> versions = GlpiMetadata.Instance.HlVersions;
        string latest = versions[^1];
        if (string.IsNullOrWhiteSpace(requested))
        {
            return latest;
        }
        string? best = null;
        foreach (string v in versions)
        {
            if (v.StartsWith(requested, StringComparison.Ordinal) && (best is null || new Version(v) > new Version(best)))
            {
                best = v;
            }
        }
        return best ?? latest;
    }

    private JsonArray ApiVersions()
    {
        string root = $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}/api.php";
        JsonArray list = [new JsonObject
        {
            ["api_version"] = "1",
            ["version"] = "1.0.0",
            ["description"] = "The low-level API which is closely tied to the GLPI source code. While not as user friendly as the high-level API, it is more powerful and allows to do some things that are not possible with the high-level API. It has no promise of stability between versions so it may change without warning.",
            ["endpoint"] = root + "/v1",
        }];
        IReadOnlyList<string> versions = GlpiMetadata.Instance.HlVersions;
        foreach (string v in versions)
        {
            JsonObject entry = new()
            {
                ["api_version"] = "2",
                ["version"] = v,
                ["endpoint"] = root + "/v" + string.Join('.', v.Split('.').Take(2)),
            };
            if (v != versions[^1])
            {
                entry["deprecated"] = true;
            }
            list.Add(entry);
        }
        return list;
    }

    private string ApiRoot() => $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}/api.php";

    private async Task ReadParametersAsync(string method)
    {
        JsonObject parameters = PhpParams.Parse(http.Request.QueryString.Value);
        if (method is not ("GET" or "DELETE" or "OPTIONS"))
        {
            string contentType = (http.Request.ContentType ?? string.Empty).Split(';')[0].Trim();
            if (contentType == "application/json")
            {
                using StreamReader reader = new(http.Request.Body, Encoding.UTF8);
                string body = await reader.ReadToEndAsync(_ct);
                if (body.Length > 0)
                {
                    try
                    {
                        if (JsonNode.Parse(body) is not JsonObject obj)
                        {
                            throw new JsonException();
                        }
                        foreach ((string key, JsonNode? value) in obj)
                        {
                            if (key != "_mandatory_filter")
                            {
                                parameters[key] = value?.DeepClone();
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        throw new HlApiException(400, "ERROR", "Invalid JSON body");
                    }
                }
            }
            else if (http.Request.HasFormContentType)
            {
                foreach ((string key, JsonNode? value) in PhpParams.ParseForm(await http.Request.ReadFormAsync(_ct)))
                {
                    parameters[key] = value?.DeepClone();
                }
            }
        }
        parameters.Remove("_mandatory_filter");
        _params = parameters;
    }

    private string? Param(string key) => _params[key] switch
    {
        null => null,
        JsonArray a => string.Join(';', a.Select(v => v?.ToString())),
        JsonObject o => string.Join(';', o.Select(v => v.Value?.ToString())),
        JsonNode n => GlpiValue.ToText(GlpiValue.FromJson(n)),
    };

    // --- Authentification ---------------------------------------------------------------------

    /// <summary>Jeton Bearer émis par /token, puis session temporaire (Router::startTemporarySession).</summary>
    private async Task<bool> AuthenticateAsync()
    {
        string authorization = http.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        AuthenticateResult result = await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!result.Succeeded || !int.TryParse(result.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
        {
            if (result.Succeeded)
            {
                // Jeton client_credentials, sans utilisateur : GLPI le réserve aux scopes sans session.
                _scopes = [.. result.Principal.FindAll(OAuthTokenConstants.ScopeClaimType).Select(c => c.Value)];
            }
            return false;
        }
        _scopes = [.. result.Principal.FindAll(OAuthTokenConstants.ScopeClaimType).Select(c => c.Value)];

        await using GlpiNgDbContext db = http.RequestServices.GetRequiredService<IRootDbContextFactory>().CreateDbContext();
        GlpiUser? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, _ct);
        if (user is null)
        {
            return false;
        }

        GlpiApiSession session = await access.OpenSessionAsync(user, null, GlpiApiSessionStore.NewToken(), _ct);
        if (int.TryParse(http.Request.Headers["GLPI-Profile"].ToString(), out int profileId) && session.Profiles.Any(p => p.Id == profileId))
        {
            session.ActiveProfileId = profileId;
        }
        if (int.TryParse(http.Request.Headers["GLPI-Entity"].ToString(), out int entityApiId))
        {
            int root = http.RequestServices.GetRequiredService<EntityTreeCache>().GetRootEntityId() ?? 0;
            int entityId = entityApiId == 0 ? root : entityApiId;
            EntityTreeCache tree = http.RequestServices.GetRequiredService<EntityTreeCache>();
            if (session.IsAdmin || session.ActiveProfileHabilitations.Any(h => h.EntityId == entityId || (h.IsRecursive && tree.IsSelfOrDescendant(h.EntityId, entityId))))
            {
                session.ActiveEntityId = entityId;
                session.ActiveEntityRecursive = http.Request.Headers["GLPI-Entity-Recursive"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
        await access.AttachAsync(http, session, _ct);
        _session = session;
        return true;
    }

    private async Task<GlpiDataContext> DataAsync() => _data ??= await access.CreateDataContextAsync(http, _session!, _ct);

    /// <summary>POST /token : délègue au point de jetons OAuth2 de GlpiNg, en acceptant aussi un corps JSON.</summary>
    private async Task TokenAsync()
    {
        OAuthController controller = new(
            http.RequestServices.GetRequiredService<GlpiNgDbContext>(),
            http.RequestServices.GetRequiredService<UserCredentialAuthenticator>(),
            http.RequestServices.GetRequiredService<OAuthTokenIssuer>())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        IActionResult result = await controller.Token(Param("grant_type"), Param("client_id"), Param("client_secret"),
            Param("username"), Param("password"), Param("scope"), _ct);
        await result.ExecuteResultAsync(new ActionContext(http, new RouteData(), new ActionDescriptor()));
    }

    // --- Routes de session et de service -------------------------------------------------------

    /// <summary>GET /session (CoreController::getSession).</summary>
    private async Task<JsonObject> SessionAsync()
    {
        GlpiApiSession session = _session!;
        GlpiDataContext data = await DataAsync();
        int[] active = access.ActiveEntityIds(session);
        int activeEntity = session.ActiveEntityId ?? session.ActiveProfileHabilitations.Select(h => h.EntityId).FirstOrDefault(data.RootEntityId);
        string entityName = await GlpiNames.DropdownNameAsync(data, "glpi_entities", data.ToApiEntity(activeEntity), _ct);
        List<int> groups = await data.Db.GroupUsers.AsNoTracking().Where(g => g.UserId == session.UserId).Select(g => g.GroupId).ToListAsync(_ct);

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

        (int profileId, string profileName) = session.Profiles.FirstOrDefault(p => p.Id == session.ActiveProfileId);
        GlpiProfile? profile = await data.Db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId, _ct);
        JsonObject activeProfile = new()
        {
            ["id"] = profileId,
            ["name"] = profileName,
            ["interface"] = "central",
            ["ticket_status"] = AllTransitions([1, 10, 2, 3, 4, 5, 6]),
            ["change_status"] = AllTransitions([1, 9, 10, 7, 4, 11, 12, 5, 8, 6, 14, 13]),
            ["problem_status"] = AllTransitions([1, 7, 2, 3, 4, 5, 8, 6]),
        };
        if (new Version(_version) >= new Version(2, 2))
        {
            ProfileRights rights = session.IsAdmin || profile is null ? ProfileRights.Full : new ProfileRights(s => GlpiApiProfiles.LevelOf(profile, s));
            activeProfile["rights"] = GlpiApiProfiles.RightsOf(rights);
        }
        if (new Version(_version) >= new Version(2, 3))
        {
            activeProfile["helpdesk_hardware"] = 0;
            activeProfile["helpdesk_item_type"] = "[]";
            activeProfile["managed_domainrecordtypes"] = "[]";
        }

        DateTime now = data.ToUserTime(DateTime.UtcNow);
        return new JsonObject
        {
            ["current_time"] = new DateTimeOffset(now, data.TimeZone.GetUtcOffset(now)).ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
            ["user_id"] = session.UserId,
            ["use_mode"] = 0,
            ["friendly_name"] = session.DisplayName,
            ["name"] = session.UserName,
            ["real_name"] = session.LastName ?? string.Empty,
            ["first_name"] = session.FirstName ?? string.Empty,
            ["default_entity"] = data.ToApiEntity(activeEntity),
            ["profiles"] = profiles,
            ["active_entities"] = activeEntities,
            ["groups"] = new JsonArray([.. groups.Select(g => (JsonNode)g)]),
            ["active_profile"] = activeProfile,
            ["active_entity"] = new JsonObject
            {
                ["id"] = data.ToApiEntity(activeEntity),
                ["short_name"] = entityName.Split(" > ").Last(),
                ["complete_name"] = entityName,
                ["recursive"] = session.ActiveEntityId is null || session.ActiveEntityRecursive ? 1 : 0,
            },
        };
    }

    private static string AllTransitions(int[] statuses)
    {
        JsonObject matrix = [];
        foreach (int from in statuses)
        {
            JsonObject row = [];
            foreach (int to in statuses)
            {
                row[to.ToString(CultureInfo.InvariantCulture)] = true;
            }
            matrix[from.ToString(CultureInfo.InvariantCulture)] = row;
        }
        return matrix.ToJsonString();
    }

    /// <summary>GET /Session/EntityTree.</summary>
    private async Task<JsonArray> EntityTreeAsync()
    {
        GlpiDataContext data = await DataAsync();
        IGlpiTable entities = data.Table("glpi_entities");
        Dictionary<int, object?> names = await entities.GetColumnAsync(data, "name", null, _ct);
        Dictionary<int, object?> parents = await entities.GetColumnAsync(data, "entities_id", null, _ct);
        int active = data.ActiveApiEntity;
        HashSet<int> ancestors = [];
        for (int? current = parents.GetValueOrDefault(active) is object p && GlpiValue.TryToLong(p, out long pl) ? (int)pl : null;
             current is int c && ancestors.Add(c);
             current = parents.GetValueOrDefault(c) is object pp && GlpiValue.TryToLong(pp, out long ppl) && ppl != c ? (int)ppl : null)
        {
        }

        JsonObject Node(int id)
        {
            JsonArray children = [.. parents.Where(kv => kv.Key != id && GlpiValue.TryToLong(kv.Value, out long parent) && parent == id)
                .Select(kv => (JsonNode)Node(kv.Key))];
            JsonObject node = new() { ["key"] = id, ["label"] = GlpiValue.ToText(names.GetValueOrDefault(id)), ["children"] = children };
            if (ancestors.Contains(id))
            {
                node["expanded"] = "true";
            }
            if (id == active)
            {
                node["selected"] = "true";
            }
            return node;
        }

        JsonArray tree = [];
        foreach (GlpiApiHabilitation h in _session!.ActiveProfileHabilitations.DistinctBy(h => h.EntityId))
        {
            int apiId = data.ToApiEntity(h.EntityId);
            tree.Add(h.IsRecursive ? Node(apiId) : new JsonObject { ["key"] = apiId, ["label"] = h.EntityName, ["children"] = new JsonArray() });
        }
        return tree;
    }

    private static JsonObject StatusAll() => new()
    {
        ["db"] = new JsonObject { ["status"] = "OK" },
        ["cas"] = new JsonObject { ["status"] = "NO_DATA" },
        ["ldap"] = new JsonObject { ["status"] = "NO_DATA" },
        ["imap"] = new JsonObject { ["status"] = "NO_DATA" },
        ["mail_collectors"] = new JsonObject { ["status"] = "NO_DATA" },
        ["crontasks"] = new JsonObject { ["status"] = "OK" },
        ["filesystem"] = new JsonObject { ["status"] = "OK" },
        ["plugins"] = new JsonObject { ["status"] = "NO_DATA" },
    };

    private async Task DocumentationAsync()
    {
        string root = http.Request.PathBase.ToString();
        JsonArray urls = [.. GlpiMetadata.Instance.HlVersions
            .OrderByDescending(v => v == _version).ThenByDescending(v => new Version(v))
            .Select(v => (JsonNode)new JsonObject
            {
                ["url"] = $"{root}/api.php/v{v}/doc.json",
                ["name"] = "v" + v + (v != GlpiMetadata.Instance.HlVersions[^1] ? " (deprecated)" : string.Empty),
                ["version"] = v,
            })];
        string html = $$"""
            <!DOCTYPE html><html lang="en"><head><meta charset="UTF-8"><title>GLPI API Documentation</title>
            <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui.css">
            <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
            <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-standalone-preset.js"></script>
            </head><body style="margin:0; padding:0;"><div id="swagger-ui"></div><script>
            window.SwaggerUIBundle({ urls: {{urls.ToJsonString()}}, dom_id: '#swagger-ui',
              presets: [SwaggerUIBundle.presets.apis, SwaggerUIStandalonePreset], layout: 'StandaloneLayout',
              docExpansion: 'none', validatorUrl: 'none', filter: true, showExtensions: true });
            </script></body></html>
            """;
        http.Response.Headers.CacheControl = "public, max-age=86400";
        await RespondRaw(html, 200, "text/html");
    }

    private void Options()
    {
        string[] methods = ["GET", "POST", "PATCH", "PUT", "DELETE"];
        if (http.Request.Headers.ContainsKey("Access-Control-Request-Method"))
        {
            http.Response.Headers["Access-Control-Allow-Methods"] = string.Join(", ", methods);
        }
        if (http.Request.Headers.ContainsKey("Access-Control-Request-Headers"))
        {
            http.Response.Headers["Access-Control-Allow-Headers"] =
                "Content-Type, Authorization, Origin, Accept, GLPI-API-Version, GLPI-Profile, GLPI-Entity, GLPI-Entity-Recursive, X-Debug-Mode";
        }
        http.Response.Headers["Access-Control-Max-Age"] = "600";
        http.Response.StatusCode = 204;
    }

    // --- Routes génériques ----------------------------------------------------------------------

    private static List<HlRoute> Routes(string version) => RoutesByVersion.GetOrAdd(version, v =>
    {
        JsonObject paths = GlpiMetadata.Instance.OpenApi(v)["paths"]!.AsObject();
        List<HlRoute> routes = [];
        foreach ((string template, JsonNode? ops) in paths)
        {
            if (template.Contains("{req}", StringComparison.Ordinal))
            {
                continue;
            }
            List<string> parameters = [];
            string pattern = "^" + Regex.Replace(Regex.Escape(template).Replace(@"\{", "{"), "{([^}]+)}", m =>
            {
                parameters.Add(m.Groups[1].Value);
                return "([^/]+)";
            }) + "$";
            routes.Add(new HlRoute(template, new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), parameters, ops!.AsObject()));
        }
        // Les chemins littéraux passent avant ceux à paramètres, comme les priorités du routeur de GLPI.
        return [.. routes.OrderBy(r => r.Parameters.Count).ThenByDescending(r => r.Template.Length)];
    });

    private static bool IsIndexRoute(string path) => path.EndsWith('/') && Routes(GlpiMetadata.Instance.HlVersions[^1]).Any(r => r.Template == path);

    private async Task DispatchAsync(string path, string method, bool head)
    {
        HlRoute? route = null;
        Match? match = null;
        foreach (HlRoute candidate in Routes(_version))
        {
            Match m = candidate.Pattern.Match(path);
            if (m.Success && candidate.Operations.ContainsKey(method.ToLowerInvariant()))
            {
                route = candidate;
                match = m;
                break;
            }
        }
        if (route is null || match is null)
        {
            throw HlApiException.NotFound();
        }

        JsonObject operation = route.Operations[method.ToLowerInvariant()]!.AsObject();
        CheckScopes(operation);

        Dictionary<string, string> values = [];
        for (int i = 0; i < route.Parameters.Count; i++)
        {
            values[route.Parameters[i]] = Uri.UnescapeDataString(match.Groups[i + 1].Value);
        }

        // Liste des types d'une famille (/Assets/, /Dropdowns/, /Components...).
        if (method == "GET" && SchemaRef(operation) is null && route.Parameters.Count == 0)
        {
            await Respond(Index(route.Template));
            return;
        }

        string schemaName = SchemaRef(operation) ?? SchemaRef(route.Operations["get"] as JsonObject)
            ?? throw HlApiException.NotFound();
        JsonObject schema = Schema(schemaName, values);
        bool isList = operation["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["type"]?.ToString() == "array";

        GlpiDataContext data = await DataAsync();
        HlEngine engine = new(data, schema);

        if (schema["x-subtypes"] is JsonArray && method == "GET")
        {
            await UnionSearchAsync(schema);
            return;
        }
        if (engine.Table.Length == 0)
        {
            throw new HlApiException(501, "ERROR", "This endpoint is not supported by GlpiNg");
        }
        if (!CanView(engine.Table))
        {
            throw HlApiException.AccessDenied();
        }

        (string? ownParam, GlpiFilter scope, GlpiInput scopeInput) = Scope(route, values, engine);
        GlpiFilter? own = ownParam is null ? null : OwnFilter(ownParam, values[ownParam], engine);
        IGlpiTable table = data.Table(engine.Table);

        switch (method)
        {
            case "GET":
                if (own is null && isList)
                {
                    int start = (int)GlpiValue.FromJson(_params["start"] ?? 0).ToLong();
                    int? limit = _params["limit"] is JsonNode l ? (int)GlpiValue.FromJson(l).ToLong() : 100;
                    GlpiFilter? rsql = await engine.FilterAsync(Param("filter"), _ct);
                    (JsonArray results, int s, int lim, int total) = await SearchScopedAsync(engine, GlpiFilter.And(scope, rsql), start, limit, Param("sort"));
                    bool hasMore = s + lim < total;
                    int end = Math.Max(0, s + lim - 1);
                    if (end > total)
                    {
                        end = total - 1;
                    }
                    http.Response.Headers["Content-Range"] = $"{s}-{end}/{total}";
                    await Respond(head ? null : results, hasMore ? 206 : 200);
                    return;
                }
                await RespondOne(engine, GlpiFilter.And(scope, own), head);
                return;

            case "POST":
            {
                GlpiInput input = engine.Input(_params, isCreate: true);
                foreach ((string key, JsonNode? value) in scopeInput)
                {
                    input[key] = value;
                }
                int id;
                try
                {
                    id = await table.InsertAsync(data, input, _ct);
                }
                catch (GlpiWriteException e) when (!e.IsRightError)
                {
                    throw CrudError("create", e.Message);
                }
                string href = path.TrimEnd('/') + "/" + id;
                http.Response.Headers.Location = href;
                await Respond(new JsonObject { ["id"] = id, ["href"] = href }, 201);
                return;
            }

            case "PATCH":
            case "PUT":
            {
                int id = await ExistingIdAsync(engine, GlpiFilter.And(scope, own));
                GlpiInput input = engine.Input(_params, isCreate: false);
                try
                {
                    await table.UpdateAsync(data, id, input, _ct);
                }
                catch (GlpiWriteException e) when (!e.IsRightError)
                {
                    throw CrudError("update", e.Message);
                }
                await RespondOne(engine, GlpiFilter.Col("id", GlpiOp.Eq, id), head: false);
                return;
            }

            case "DELETE":
            {
                int id = await ExistingIdAsync(engine, GlpiFilter.And(scope, own));
                bool force = GlpiValue.TryToBool(GlpiValue.FromJson(_params["force"]), out bool f) && f;
                try
                {
                    await table.DeleteAsync(data, id, force || !table.HasTrash, _ct);
                }
                catch (GlpiWriteException e) when (!e.IsRightError)
                {
                    throw CrudError("delete", e.Message);
                }
                http.Response.StatusCode = 204;
                return;
            }

            default:
                throw HlApiException.NotFound();
        }
    }

    private static HlApiException CrudError(string action, string message)
        => new(500, "ERROR", $"Failed to {action} item(s)", null) { };

    private async Task<(JsonArray, int, int, int)> SearchScopedAsync(HlEngine engine, GlpiFilter? scope, int start, int? limit, string? sort)
    {
        // Le périmètre (parent, identifiant) est déjà une condition GLPI ; le filtre RSQL aussi.
        (JsonArray results, int s, int l, int total) = await engine.SearchAsync(null, null, start, limit, sort, _ct, scope);
        return (results, s, l, total);
    }

    private async Task RespondOne(HlEngine engine, GlpiFilter filter, bool head)
    {
        (JsonArray results, _, _, _) = await engine.SearchAsync(null, null, 0, 1, null, _ct, filter);
        if (results.Count == 0)
        {
            throw HlApiException.NotFound();
        }
        await Respond(head ? null : results[0]);
    }

    private async Task OneAsync(string schemaName, GlpiFilter filter)
    {
        JsonObject schema = Schema(schemaName, []);
        HlEngine engine = new(await DataAsync(), schema);
        await RespondOne(engine, filter, head: false);
    }

    private async Task<int> ExistingIdAsync(HlEngine engine, GlpiFilter filter)
    {
        GlpiDataContext data = await DataAsync();
        GlpiPage page = await data.Table(engine.Table).QueryAsync(data, filter, null, 0, 1, _ct);
        return page.Ids.Count > 0 ? page.Ids[0] : throw HlApiException.NotFound();
    }

    private async Task UnionSearchAsync(JsonObject union)
    {
        // /Assets/Global : la recherche sur chaque type d'actif, résultats mis bout à bout.
        GlpiDataContext data = await DataAsync();
        JsonArray all = [];
        foreach (JsonObject subtype in union["x-subtypes"]!.AsArray().OfType<JsonObject>())
        {
            if (GlpiMetadata.Instance.HlSchemas[_version][(string)subtype["schema_name"]!] is JsonObject sub)
            {
                HlEngine engine = new(data, sub);
                if (engine.Table.Length == 0 || !data.Catalog.IsBacked(engine.Table) || !CanView(engine.Table))
                {
                    continue;
                }
                (JsonArray results, _, _, _) = await engine.SearchAsync(Param("filter"), null, 0, null, Param("sort"), _ct);
                foreach (JsonNode? r in results)
                {
                    if (r is JsonObject o)
                    {
                        o["_itemtype"] = (string?)subtype["itemtype"];
                        all.Add(o.DeepClone());
                    }
                }
            }
        }
        int start = (int)GlpiValue.FromJson(_params["start"] ?? 0).ToLong();
        int limit = _params["limit"] is JsonNode l ? (int)GlpiValue.FromJson(l).ToLong() : 100;
        JsonArray page = [.. all.Skip(start).Take(limit).Select(n => n?.DeepClone())];
        int end = Math.Max(0, start + limit - 1);
        if (end > all.Count)
        {
            end = all.Count - 1;
        }
        http.Response.Headers["Content-Range"] = $"{start}-{end}/{all.Count}";
        await Respond(page, start + limit < all.Count ? 206 : 200);
    }

    /// <summary>Liste des types sous une famille de routes : les routes sœurs qui rendent une liste d'un schéma.</summary>
    private JsonArray Index(string template)
    {
        string prefix = template.TrimEnd('/') + "/";
        JsonArray list = [];
        foreach (HlRoute route in Routes(_version).Where(r => r.Parameters.Count == 0 && r.Template.StartsWith(prefix, StringComparison.Ordinal)
                     && r.Template.Length > prefix.Length && !r.Template[prefix.Length..].Contains('/')))
        {
            if (route.Operations["get"] is not JsonObject get || SchemaRef(get) is not string schemaName
                || GlpiMetadata.Instance.HlSchemas[_version][schemaName] is not JsonObject schema
                || schema["x-itemtype"] is null)
            {
                continue;
            }
            list.Add(new JsonObject
            {
                ["itemtype"] = (string?)schema["x-itemtype"],
                ["name"] = GlpiMetadata.Instance.FindItemtype((string?)schema["x-itemtype"])?.Name ?? schemaName,
                ["href"] = route.Template,
            });
        }
        return list;
    }

    private static string? SchemaRef(JsonObject? operation)
    {
        JsonNode? schema = operation?["responses"]?["200"]?["content"]?["application/json"]?["schema"]
            ?? operation?["requestBody"]?["content"]?["application/json"]?["schema"];
        string? reference = (string?)(schema?["$ref"] ?? schema?["items"]?["$ref"]);
        return reference?.Split('/').Last();
    }

    private JsonObject Schema(string name, Dictionary<string, string> values)
    {
        Dictionary<string, JsonObject> schemas = GlpiMetadata.Instance.HlSchemas[_version]
            .ToDictionary(p => p.Key, p => p.Value!.AsObject(), StringComparer.OrdinalIgnoreCase);
        if (schemas.TryGetValue(name, out JsonObject? schema))
        {
            return schema;
        }
        // Schéma dont le nom dépend d'un paramètre de chemin (types d'actifs personnalisés...).
        foreach ((string key, string value) in values)
        {
            if (schemas.TryGetValue(name.Replace("{" + key + "}", value), out schema))
            {
                return schema;
            }
        }
        throw HlApiException.NotFound();
    }

    /// <summary>
    /// Périmètre d'une route : paramètre désignant l'élément lui-même, et condition / valeurs
    /// d'écriture tirées des paramètres désignant un parent (/Assets/Computer/{asset_id}/Antivirus,
    /// /Assets/Cartridge/{cartridgeitems_id}/{id}...), comme les mandatory filters des contrôleurs de GLPI.
    /// </summary>
    private (string? Own, GlpiFilter Scope, GlpiInput Input) Scope(HlRoute route, Dictionary<string, string> values, HlEngine engine)
    {
        string[] segments = route.Template.Trim('/').Split('/');
        string? own = null;
        if (segments[^1].StartsWith('{') && route.Parameters.Count > 0)
        {
            own = route.Parameters[^1];
        }
        else if (route.Template.EndsWith("/username/{username}", StringComparison.Ordinal))
        {
            own = "username";
        }

        List<GlpiFilter> filters = [];
        GlpiInput input = [];
        HashSet<string> columns = [.. GlpiMetadata.Instance.ColumnsOf(engine.Table).Select(c => c.Name)];

        for (int i = 0; i < segments.Length; i++)
        {
            if (!segments[i].StartsWith('{'))
            {
                continue;
            }
            string param = segments[i][1..^1];
            if (param == own || param.EndsWith("itemtype", StringComparison.Ordinal) || param is "component_type" or "role" or "service")
            {
                continue;
            }
            string value = values[param];
            string? parentType = i > 0 ? (segments[i - 1].StartsWith('{') ? values.GetValueOrDefault(segments[i - 1][1..^1]) : segments[i - 1]) : null;
            string plural = param.EndsWith("_id", StringComparison.Ordinal) ? param[..^3] + "s_id" : param;

            if (columns.Contains(param))
            {
                filters.Add(GlpiFilter.Col(param, GlpiOp.Eq, value));
                input[param] = value;
            }
            else if (columns.Contains(plural))
            {
                filters.Add(GlpiFilter.Col(plural, GlpiOp.Eq, value));
                input[plural] = value;
            }
            else if (parentType is not null && columns.Contains("itemtype") && columns.Contains("items_id"))
            {
                string itemtype = GlpiMetadata.Instance.FindItemtype(parentType)?.Name ?? parentType;
                filters.Add(GlpiFilter.And(GlpiFilter.Col("itemtype", GlpiOp.Eq, itemtype), GlpiFilter.Col("items_id", GlpiOp.Eq, value)));
                input["itemtype"] = itemtype;
                input["items_id"] = value;
            }
            else if (parentType is not null && GlpiNames.ForeignKeyForItemtype(parentType) is string fk && columns.Contains(fk))
            {
                filters.Add(GlpiFilter.Col(fk, GlpiOp.Eq, value));
                input[fk] = value;
            }
            else
            {
                throw new HlApiException(501, "ERROR", "This endpoint is not supported by GlpiNg");
            }
        }
        return (own, GlpiFilter.And([.. filters]), input);
    }

    private static GlpiFilter OwnFilter(string param, string value, HlEngine engine)
    {
        if (param == "username")
        {
            return engine.Locate("username") is { } location ? GlpiFilter.Col(location.Column, GlpiOp.Eq, value) : GlpiFilter.False;
        }
        return GlpiFilter.Col("id", GlpiOp.Eq, value);
    }

    private bool CanView(string table)
    {
        Type? type = _data?.Table(table).ClrType;
        return type is null || _data!.Rights.CanRead(ProfileSectionMap.ForEntityType(type));
    }

    private void CheckScopes(JsonObject operation)
    {
        List<string> required = [.. (operation["security"] as JsonArray ?? [])
            .OfType<JsonObject>().SelectMany(s => s["oauth"] as JsonArray ?? []).Select(s => s?.ToString() ?? string.Empty)];
        if (required.Count > 0 && !required.Any(_scopes.Contains))
        {
            throw HlApiException.AccessDenied();
        }
    }

    // --- Réponses -------------------------------------------------------------------------------

    private static JsonObject Error(string status, string title, JsonNode? detail = null) => new()
    {
        ["status"] = status,
        ["title"] = title,
        ["detail"] = detail?.DeepClone(),
    };

    private async Task Respond(JsonNode? body, int code = 200)
    {
        http.Response.StatusCode = code;
        http.Response.ContentType = "application/json";
        await http.Response.WriteAsync(body is null ? "null" : body.ToJsonString(Json), _ct);
    }

    private async Task RespondRaw(string content, int code, string contentType)
    {
        http.Response.StatusCode = code;
        http.Response.ContentType = contentType;
        await http.Response.WriteAsync(content, _ct);
    }
}
