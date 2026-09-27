using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GlpiNg.Web.Api.Glpi;
using GlpiNg.Web.Api.Glpi.Data;

namespace GlpiNg.Web.Api.HighLevel;

/// <summary>Réponse d'erreur de l'API v2 : <c>{ status, title, detail }</c>.</summary>
public sealed class HlApiException(int httpCode, string status, string title, JsonNode? detail = null) : Exception(title)
{
    public int HttpCode { get; } = httpCode;
    public string Status { get; } = status;
    public string Title { get; } = title;
    public JsonNode? Detail { get; } = detail;

    public static HlApiException NotFound() => new(404, "ERROR_ITEM_NOT_FOUND", "Not found");

    public static HlApiException AccessDenied(string? detail = null)
        => new(403, "ERROR_RIGHT_MISSING", "You don't have permission to perform this action.", detail is null ? null : JsonValue.Create(detail));

    public static HlApiException Invalid(string title, JsonNode? detail = null) => new(400, "ERROR_INVALID_PARAMETER", title, detail);
}

/// <summary>Où lire une propriété d'un schéma : colonne de la table principale, ou colonne d'une table jointe.</summary>
public sealed record HlLocation(IReadOnlyList<GlpiJoinStep> Steps, string Table, string Column, JsonObject Property, bool IsMapped);

/// <summary>
/// Moteur de l'API v2 : les schémas de GLPI (propriétés, x-field, x-join, ref-join...) évalués sur
/// les tables GLPI virtuelles — recherche RSQL, tri, pagination, rendu typé, et traduction des
/// corps d'écriture en colonnes GLPI. C'est l'équivalent de Glpi\Api\HL\Search et ResourceAccessor.
/// </summary>
public sealed partial class HlEngine(GlpiDataContext ctx, JsonObject schema)
{
    public JsonObject Schema { get; } = schema;

    public string Itemtype { get; } = (string?)schema["x-itemtype"]
        ?? GlpiMetadata.Instance.ItemtypeForTable((string?)schema["x-table"] ?? string.Empty) ?? string.Empty;

    public string Table { get; } = (string?)schema["x-table"]
        ?? GlpiMetadata.Instance.FindItemtype((string?)schema["x-itemtype"])?.Table ?? string.Empty;

    private JsonObject Properties => Schema["properties"] as JsonObject ?? [];

    // --- Résolution des propriétés -------------------------------------------------------------

    /// <summary>Emplacement d'une propriété désignée par son chemin (« entity.name »), ou null si inconnue.</summary>
    public HlLocation? Locate(string path)
    {
        string[] parts = path.Split('.');
        JsonObject props = Properties;
        List<GlpiJoinStep> steps = [];
        string table = Table;
        JsonObject? prop = null;

        for (int i = 0; i < parts.Length; i++)
        {
            if (props[parts[i]] is not JsonObject p)
            {
                return null;
            }
            prop = p;
            if (p["x-mapped-from"] is not null)
            {
                return new HlLocation(steps, table, parts[i], p, IsMapped: true);
            }

            JsonObject? join = p["x-join"] as JsonObject ?? p["items"]?["x-join"] as JsonObject;
            if (join is not null && i < parts.Length - 1)
            {
                // « entity.id » sur une jointure standard : la clé étrangère de la table courante suffit.
                if (i == parts.Length - 2 && parts[^1] == "id" && join["ref-join"] is null && (string?)join["field"] == "id"
                    && JoinCondition(join) is null)
                {
                    return new HlLocation(steps, table, (string?)join["fkey"] ?? parts[i], (JsonObject)(p["properties"]?["id"] ?? p["items"]?["properties"]?["id"] ?? new JsonObject { ["type"] = "integer" }), IsMapped: false);
                }
                List<GlpiJoinStep>? joinSteps = JoinSteps(join);
                if (joinSteps is null)
                {
                    return null;
                }
                steps.AddRange(joinSteps);
                table = (string)join["table"]!;
                props = (p["properties"] ?? p["items"]?["properties"]) as JsonObject ?? [];
                continue;
            }

            if (join is null && p["properties"] is JsonObject nested && i < parts.Length - 1)
            {
                props = nested;
                continue;
            }
            if (i < parts.Length - 1)
            {
                return null;
            }
        }

        if (prop is null || prop["type"]?.ToString() is "object" or "array")
        {
            return null;
        }
        return new HlLocation(steps, table, (string?)prop["x-field"] ?? parts[^1], prop, IsMapped: false);
    }

    /// <summary>x-join (et son éventuel ref-join) en étapes de jointure ; null si la forme n'est pas gérée.</summary>
    public static List<GlpiJoinStep>? JoinSteps(JsonObject join)
    {
        List<GlpiJoinStep> steps = [];
        if (join["ref-join"] is JsonObject refJoin)
        {
            GlpiJoinStep? first = Hop((string?)refJoin["fkey"], (string)refJoin["table"]!, (string?)refJoin["field"], JoinCondition(refJoin));
            if (first is null)
            {
                return null;
            }
            steps.Add(first);
        }
        GlpiJoinStep? last = Hop((string?)join["fkey"], (string)join["table"]!, (string?)join["field"], JoinCondition(join));
        if (last is null)
        {
            return null;
        }
        steps.Add(last);
        return steps;
    }

    private static GlpiJoinStep? Hop(string? fkey, string table, string? field, GlpiFilter? condition)
    {
        if (field == "id" && fkey is not null)
        {
            return new GlpiJoinStep(table, fkey, null, condition);
        }
        if (fkey == "id" && field is not null)
        {
            return new GlpiJoinStep(table, null, field, condition);
        }
        return null;
    }

    private static GlpiFilter? JoinCondition(JsonObject join)
    {
        if (join["condition"] is not JsonObject condition)
        {
            return null;
        }
        List<GlpiFilter> filters = [];
        foreach ((string column, JsonNode? value) in condition)
        {
            if (value is JsonValue)
            {
                filters.Add(GlpiFilter.Col(column, GlpiOp.Eq, GlpiValue.FromJson(value)));
            }
            else if (value is JsonArray list)
            {
                filters.Add(GlpiFilter.Col(column, GlpiOp.In, list.Select(GlpiValue.FromJson).ToList()));
            }
        }
        return filters.Count == 0 ? null : GlpiFilter.And([.. filters]);
    }

    // --- Recherche -----------------------------------------------------------------------------

    /// <summary>Search::getSearchResultsBySchema.</summary>
    public async Task<(JsonArray Results, int Start, int Limit, int Total)> SearchAsync(string? filter, string? mandatoryFilter,
        int start, int? limit, string? sort, CancellationToken ct, GlpiFilter? scope = null)
    {
        IGlpiTable table = ctx.Table(Table);
        GlpiFilter condition = GlpiFilter.And(
            scope,
            await FilterAsync(mandatoryFilter, ct),
            await FilterAsync(filter, ct));

        // Tri : poussé en base s'il ne porte que sur des colonnes de la table principale.
        List<(HlLocation Location, bool Descending)> sorts = [];
        foreach (string s in (sort ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = s.Split(':');
            HlLocation location = Locate(parts[0]) is { IsMapped: false } l
                ? l
                : throw new HlApiException(400, "ERROR", "Invalid property for sorting: " + parts[0]);
            sorts.Add((location, parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)));
        }

        List<int> page;
        int total;
        if (sorts.All(s => s.Location.Steps.Count == 0))
        {
            GlpiPage result = await table.QueryAsync(ctx, condition, [.. sorts.Select(s => new GlpiSort(s.Location.Column, s.Descending))], start, limit, ct);
            (page, total) = ([.. result.Ids], result.Total);
        }
        else
        {
            List<int> ids = [.. (await table.QueryAsync(ctx, condition, null, 0, null, ct)).Ids];
            total = ids.Count;
            IComparer<object?> comparer = Comparer<object?>.Create(GlpiValue.Compare);
            IOrderedEnumerable<int> ordered = ids.OrderBy(_ => 0);
            foreach ((HlLocation location, bool descending) in sorts)
            {
                GlpiSearchPath path = new() { Steps = location.Steps, Table = location.Table, Field = location.Column };
                Dictionary<int, List<object?>> keys = await GlpiSearchEvaluator.ValuesAsync(ctx, Table, path, ids, ct);
                ordered = descending
                    ? ordered.ThenByDescending(id => keys[id].FirstOrDefault(), comparer)
                    : ordered.ThenBy(id => keys[id].FirstOrDefault(), comparer);
            }
            IEnumerable<int> paged = ordered.ThenBy(id => id).Skip(start);
            page = [.. limit is int l ? paged.Take(l) : paged];
        }

        JsonArray results = await RenderAsync(page, ct);
        return (results, start, limit ?? results.Count, total);
    }

    /// <summary>Condition RSQL traduite en condition sur la table principale.</summary>
    public async Task<GlpiFilter?> FilterAsync(string? rsql, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rsql))
        {
            return null;
        }
        RsqlNode? node;
        try
        {
            node = Rsql.Parse(rsql);
        }
        catch (RsqlException e)
        {
            throw HlApiException.Invalid(e.Message);
        }

        JsonObject invalid = [];
        CollectInvalid(node, invalid);
        if (invalid.Count > 0)
        {
            throw HlApiException.Invalid("RSQL query has invalid filters", invalid);
        }
        return node is null ? null : await TranslateAsync(node, ct);
    }

    private void CollectInvalid(RsqlNode? node, JsonObject invalid)
    {
        switch (node)
        {
            case RsqlLogical l:
                foreach (RsqlNode item in l.Items)
                {
                    CollectInvalid(item, invalid);
                }
                break;
            case RsqlComparison c:
                HlLocation? location = Locate(c.Property);
                if (location is null)
                {
                    // Propriété inconnue : ignorée, comme GLPI.
                    break;
                }
                if (location.IsMapped)
                {
                    invalid[c.Property] = "Mapped properties cannot be used in RSQL";
                }
                else if (!Rsql.Operators.Contains(c.Operator))
                {
                    invalid[c.Property] = "Unknown operator";
                }
                else if (Rsql.ExpectsValue(c.Operator) && c.Value is null)
                {
                    throw HlApiException.Invalid($"RSQL query is missing a value in filter for property \"{c.Property}\"");
                }
                break;
        }
    }

    private async Task<GlpiFilter?> TranslateAsync(RsqlNode node, CancellationToken ct)
    {
        if (node is RsqlLogical logical)
        {
            List<GlpiFilter> items = [];
            foreach (RsqlNode item in logical.Items)
            {
                if (await TranslateAsync(item, ct) is GlpiFilter f)
                {
                    items.Add(f);
                }
            }
            return items.Count == 0 ? null : logical.IsAnd ? GlpiFilter.And([.. items]) : GlpiFilter.Or([.. items]);
        }

        RsqlComparison c = (RsqlComparison)node;
        if (Locate(c.Property) is not { IsMapped: false } location)
        {
            return null;
        }
        GlpiFilter target = Comparison(location, c.Operator, c.Value);
        if (location.Steps.Count == 0)
        {
            return target;
        }
        GlpiSearchPath path = new() { Steps = location.Steps, Table = location.Table, Field = location.Column };
        HashSet<int> ids = await GlpiSearchEvaluator.MatchingAsync(ctx, Table, path, target, GlpiFilter.True, ct);
        return GlpiFilter.IdIn(ids);
    }

    private GlpiFilter Comparison(HlLocation location, string op, string? raw)
    {
        string column = location.Column;
        object? value = TypedValue(location.Property, raw);
        List<object?> List() => [.. (raw ?? string.Empty).Trim('(', ')').Split(',').Select(v => TypedValue(location.Property, v.Trim().Trim('"', '\'')))];
        string Pattern() => (raw ?? string.Empty).Replace('%', '_').Replace('*', '%');

        return op switch
        {
            "==" => GlpiFilter.Col(column, GlpiOp.Eq, value),
            "!=" => GlpiFilter.Col(column, GlpiOp.Ne, value),
            "=in=" => GlpiFilter.Col(column, GlpiOp.In, List()),
            "=out=" => GlpiFilter.Col(column, GlpiOp.NotIn, List()),
            "=lt=" => GlpiFilter.Col(column, GlpiOp.Lt, value),
            "=le=" => GlpiFilter.Col(column, GlpiOp.Le, value),
            "=gt=" => GlpiFilter.Col(column, GlpiOp.Gt, value),
            "=ge=" => GlpiFilter.Col(column, GlpiOp.Ge, value),
            // ponytail: =like= est sensible à la casse chez GLPI (CAST BINARY) ; ici comme =ilike=.
            "=like=" or "=ilike=" => GlpiFilter.Col(column, GlpiOp.Like, Pattern()),
            "=notlike=" or "=notilike=" => GlpiFilter.Col(column, GlpiOp.NotLike, Pattern()),
            "=isnull=" => GlpiFilter.Col(column, GlpiOp.IsNull),
            "=notnull=" => GlpiFilter.Col(column, GlpiOp.NotNull),
            "=empty=" => GlpiFilter.Or(GlpiFilter.Col(column, GlpiOp.IsNull), GlpiFilter.Col(column, GlpiOp.Eq, string.Empty)),
            "=notempty=" => GlpiFilter.And(GlpiFilter.Col(column, GlpiOp.NotNull), GlpiFilter.Col(column, GlpiOp.Ne, string.Empty)),
            _ => GlpiFilter.True,
        };
    }

    /// <summary>Valeur d'un filtre dans le type de la propriété (booléens 0/1, dates RFC 3339 → heure locale).</summary>
    private object? TypedValue(JsonObject property, string? raw)
    {
        if (raw is null)
        {
            return null;
        }
        if (property["type"]?.ToString() == "boolean")
        {
            return raw.Trim().ToLowerInvariant() is "1" or "true" or "on" or "yes" ? 1L : 0L;
        }
        if (property["format"]?.ToString() == "date-time" && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset dto))
        {
            return ctx.ToUserTime(dto.UtcDateTime);
        }
        return raw;
    }

    // --- Rendu ---------------------------------------------------------------------------------

    /// <summary>Éléments rendus selon le schéma, dans l'ordre des identifiants.</summary>
    public async Task<JsonArray> RenderAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        Dictionary<int, GlpiRow> rows = await ctx.Table(Table).GetRowsAsync(ctx, ids, ct);
        List<int> present = [.. ids.Where(rows.ContainsKey)];
        Dictionary<string, JoinData> joins = await LoadJoinsAsync(Properties, string.Empty, present, ct);

        JsonArray results = [];
        foreach (int id in present)
        {
            JsonObject obj = await BuildAsync(Properties, string.Empty, Table, rows[id], id, joins, ct);
            results.Add(obj);
        }
        return results;
    }

    private sealed record JoinData(string Table, Dictionary<int, List<int>> Reach, Dictionary<int, GlpiRow> Rows);

    /// <summary>Lignes jointes de chaque propriété à jointure (niveau principal et objets simples imbriqués).</summary>
    private async Task<Dictionary<string, JoinData>> LoadJoinsAsync(JsonObject props, string prefix, List<int> ids, CancellationToken ct)
    {
        Dictionary<string, JoinData> result = [];
        foreach ((string name, JsonNode? node) in props)
        {
            if (node is not JsonObject p || ids.Count == 0)
            {
                continue;
            }
            JsonObject? join = p["x-join"] as JsonObject ?? p["items"]?["x-join"] as JsonObject;
            if (join is not null)
            {
                List<GlpiJoinStep>? steps = JoinSteps(join);
                if (steps is null)
                {
                    continue;
                }
                string table = (string)join["table"]!;
                Dictionary<int, List<int>> reach = await GlpiSearchEvaluator.ReachAsync(ctx, Table, steps, ids, ct);
                Dictionary<int, GlpiRow> rows = await ctx.Table(table).GetRowsAsync(ctx, [.. reach.Values.SelectMany(v => v).Distinct()], ct);
                result[prefix + name] = new JoinData(table, reach, rows);
            }
            else if (p["type"]?.ToString() == "object" && p["properties"] is JsonObject nested && p["x-mapped-from"] is null)
            {
                foreach ((string key, JoinData data) in await LoadJoinsAsync(nested, prefix + name + ".", ids, ct))
                {
                    result[key] = data;
                }
            }
        }
        return result;
    }

    private async Task<JsonObject> BuildAsync(JsonObject props, string prefix, string table, GlpiRow row, int mainId,
        Dictionary<string, JoinData>? joins, CancellationToken ct)
    {
        JsonObject obj = [];
        foreach ((string name, JsonNode? node) in props)
        {
            if (node is not JsonObject p)
            {
                continue;
            }
            string path = prefix + name;
            string type = p["type"]?.ToString() ?? "string";

            if (p["x-mapped-from"] is not null || p["items"]?["x-mapped-from"] is not null)
            {
                obj[name] = await MappedAsync(path, p, row, ct);
                continue;
            }

            JsonObject? join = p["x-join"] as JsonObject ?? p["items"]?["x-join"] as JsonObject;
            if (join is not null)
            {
                JsonObject inner = (p["properties"] ?? p["items"]?["properties"]) as JsonObject ?? [];
                if (joins is null || !joins.TryGetValue(path, out JoinData? data))
                {
                    obj[name] = type == "array" ? new JsonArray() : null;
                    continue;
                }
                List<JsonObject> members = [];
                foreach (int joinedId in data.Reach.GetValueOrDefault(mainId) ?? [])
                {
                    if (data.Rows.TryGetValue(joinedId, out GlpiRow? joinedRow))
                    {
                        members.Add(await BuildAsync(inner, string.Empty, data.Table, joinedRow, mainId, null, ct));
                    }
                }
                obj[name] = type == "array"
                    ? new JsonArray([.. members])
                    : members.Count > 0 ? members[0] : null;
                continue;
            }

            if (type == "object" && p["properties"] is JsonObject nested)
            {
                obj[name] = await BuildAsync(nested, path + ".", table, row, mainId, joins, ct);
                continue;
            }
            if (type == "array")
            {
                obj[name] = new JsonArray();
                continue;
            }

            string column = (string?)p["x-field"] ?? name;
            GlpiColumn? col = GlpiMetadata.Instance.ColumnsOf(table).FirstOrDefault(c => c.Name == column);
            object? value = row.TryGetValue(column, out object? v) ? v : col is null ? null : GlpiDefaults.DefaultValue(table, column);
            if (LegacyUndisclosed(table, column))
            {
                value = null;
            }
            obj[name] = Cast(GlpiNames.Format(col, value), p);
        }
        return obj;
    }

    private static bool LegacyUndisclosed(string table, string column) => Legacy.LegacySerializer.IsUndisclosed(table, column);

    /// <summary>Doc\Schema::castScalarProperty : type du schéma, dates au format RFC 3339.</summary>
    private JsonNode? Cast(JsonNode? raw, JsonObject property)
    {
        if (raw is null)
        {
            return null;
        }
        object? value = GlpiValue.FromJson(raw);
        JsonNode? cast = property["type"]?.ToString() switch
        {
            "integer" => JsonValue.Create(GlpiValue.TryToLong(value, out long l) ? l : 0),
            "number" => JsonValue.Create(GlpiValue.TryToDecimal(value, out decimal d) ? (double)d : 0d),
            "boolean" => JsonValue.Create(GlpiValue.TryToBool(value, out bool b) && b),
            "string" => JsonValue.Create(GlpiValue.ToText(value) ?? string.Empty),
            _ => raw.DeepClone(),
        };
        if (property["format"]?.ToString() == "date-time")
        {
            if (!GlpiValue.TryToDateTime(value, out DateTime local))
            {
                return cast;
            }
            TimeSpan offset = ctx.TimeZone.GetUtcOffset(local);
            return JsonValue.Create(new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), offset)
                .ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture));
        }
        return cast;
    }

    /// <summary>Propriétés calculées par GLPI (x-mapper, fonctions PHP) : reprises une à une.</summary>
    private async Task<JsonNode?> MappedAsync(string path, JsonObject property, GlpiRow row, CancellationToken ct)
    {
        string from = (string?)property["x-mapped-from"] ?? (string?)property["items"]?["x-mapped-from"] ?? string.Empty;

        // Libellé d'une valeur énumérée (statut d'un ticket...) : celui de la description de l'identifiant.
        if (path.EndsWith(".name", StringComparison.Ordinal) && from.EndsWith(".id", StringComparison.Ordinal))
        {
            string idPath = from;
            if (Locate(idPath) is { } location && row.GetValueOrDefault(location.Column) is object raw)
            {
                JsonObject? idProp = ResolveProperty(idPath);
                string? description = (string?)idProp?["description"];
                if (description is not null)
                {
                    Match m = Regex.Match(description, $@"^- {Regex.Escape(GlpiValue.ToText(raw) ?? string.Empty)}: (.+)$", RegexOptions.Multiline);
                    if (m.Success)
                    {
                        return m.Groups[1].Value.Trim();
                    }
                }
            }
            return null;
        }

        switch (path)
        {
            case "download_url":
                return $"/Management/Document/{row.Id}/Download";
            case "team":
                return await TeamAsync(row.Id, ct);
            case "pictures":
                return new JsonArray();
            default:
                return null;
        }
    }

    private JsonObject? ResolveProperty(string path)
    {
        JsonObject? current = null;
        JsonObject props = Properties;
        foreach (string part in path.Split('.'))
        {
            current = props[part] as JsonObject;
            if (current is null)
            {
                return null;
            }
            props = (current["properties"] ?? current["items"]?["properties"]) as JsonObject ?? [];
        }
        return current;
    }

    /// <summary>Équipe d'un objet ITIL (ITILController::getCleanTeam) : demandeur, technicien, groupe.</summary>
    private async Task<JsonArray> TeamAsync(int id, CancellationToken ct)
    {
        (string users, string groups, string fk) = Itemtype switch
        {
            "Ticket" => ("glpi_tickets_users", "glpi_groups_tickets", "tickets_id"),
            "Problem" => ("glpi_problems_users", "glpi_groups_problems", "problems_id"),
            "Change" => ("glpi_changes_users", "glpi_changes_groups", "changes_id"),
            _ => (string.Empty, string.Empty, string.Empty),
        };
        JsonArray team = [];
        if (users.Length == 0)
        {
            return team;
        }
        foreach ((string table, string itemtype, string memberColumn) in new[] { (users, "User", "users_id"), (groups, "Group", "groups_id") })
        {
            IGlpiTable links = ctx.Table(table);
            GlpiPage page = await links.QueryAsync(ctx, GlpiFilter.Col(fk, GlpiOp.Eq, id), null, 0, null, ct);
            foreach (GlpiRow link in (await links.GetRowsAsync(ctx, page.Ids, ct)).Values)
            {
                int memberId = (int)(GlpiValue.TryToLong(link.GetValueOrDefault(memberColumn), out long m) ? m : 0);
                string memberTable = itemtype == "User" ? "glpi_users" : "glpi_groups";
                GlpiRow? member = (await ctx.Table(memberTable).GetRowsAsync(ctx, [memberId], ct)).GetValueOrDefault(memberId);
                long role = GlpiValue.TryToLong(link.GetValueOrDefault("type"), out long r) ? r : 0;
                team.Add(new JsonObject
                {
                    ["role"] = role switch { 1 => "requester", 2 => "assigned", 3 => "observer", _ => role.ToString(CultureInfo.InvariantCulture) },
                    ["name"] = GlpiValue.ToText(member?.GetValueOrDefault("name")),
                    ["realname"] = GlpiValue.ToText(member?.GetValueOrDefault("realname")),
                    ["firstname"] = GlpiValue.ToText(member?.GetValueOrDefault("firstname")),
                    ["display_name"] = member is null ? null : GlpiNames.DisplayName(memberTable, member),
                    ["id"] = memberId,
                    ["href"] = itemtype == "User" ? $"/front/user.form.php?id={memberId}" : $"/front/group.form.php?id={memberId}",
                    ["type"] = itemtype,
                });
            }
        }
        return team;
    }

    // --- Écriture ------------------------------------------------------------------------------

    /// <summary>ResourceAccessor::getInputParamsBySchema et sa validation : corps de requête → colonnes GLPI.</summary>
    public GlpiInput Input(JsonObject body, bool isCreate)
    {
        JsonObject errors = [];
        GlpiInput input = [];
        Collect(Properties, body, input, errors, isCreate, string.Empty);
        if (errors.Count > 0)
        {
            throw HlApiException.Invalid("Invalid input parameters", errors);
        }
        return input;
    }

    private void Collect(JsonObject props, JsonObject? body, GlpiInput input, JsonObject errors, bool isCreate, string prefix)
    {
        foreach ((string name, JsonNode? node) in props)
        {
            if (node is not JsonObject p)
            {
                continue;
            }
            string path = prefix + name;
            JsonNode? value = body?[name];
            bool present = body is not null && body.ContainsKey(name);

            if (isCreate && p["required"]?.GetValueKind() == JsonValueKind.True && !present)
            {
                errors[path] = new JsonArray(new JsonObject { ["error"] = "required", ["message"] = "This field is required" });
                continue;
            }
            if (!present || p["x-mapped-from"] is not null)
            {
                continue;
            }

            JsonObject? join = p["x-join"] as JsonObject;
            if (join is not null)
            {
                // Seul l'identifiant d'une valeur jointe s'écrit (« entity.id », ou directement un nombre).
                JsonNode? id = value is JsonObject o ? o["id"] : value;
                if (id is not null)
                {
                    string column = (string?)p["x-input-field"] ?? (string?)p["x-field"] ?? (string?)join["fkey"] ?? name;
                    input[column] = id.DeepClone();
                }
                continue;
            }
            if (p["type"]?.ToString() == "array")
            {
                if (p["x-input-field"]?.ToString() is string field && value is JsonArray items)
                {
                    input[field] = new JsonArray([.. items.Select(i => (i is JsonObject io ? io["id"] : i)?.DeepClone())]);
                }
                continue;
            }
            if (p["type"]?.ToString() == "object" && p["properties"] is JsonObject nested)
            {
                Collect(nested, value as JsonObject, input, errors, isCreate, path + ".");
                continue;
            }
            if (p["readOnly"]?.GetValueKind() == JsonValueKind.True)
            {
                continue;
            }

            string target = (string?)p["x-input-field"] ?? (string?)p["x-field"] ?? name;
            if (Validate(path, p, value) is JsonArray problems)
            {
                errors[path] = problems;
                continue;
            }
            if (p["format"]?.ToString() == "date-time" && value is JsonValue dv && dv.GetValueKind() == JsonValueKind.String
                && DateTimeOffset.TryParse(dv.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset dto))
            {
                input[target] = GlpiValue.ToText(ctx.ToUserTime(dto.UtcDateTime));
                continue;
            }
            input[target] = value?.DeepClone();
        }
    }

    private static JsonArray? Validate(string path, JsonObject p, JsonNode? value)
    {
        JsonArray problems = [];
        object? v = GlpiValue.FromJson(value);
        if (p["maxLength"] is JsonValue max && v is string s && s.Length > (int)max)
        {
            problems.Add(new JsonObject { ["error"] = "maxLength", ["message"] = $"This field must be at most {max} characters long", ["maxLength"] = max.DeepClone() });
        }
        if (GlpiValue.TryToDecimal(v, out decimal number) && v is not string)
        {
            decimal? min = p["minimum"] is JsonValue mn ? (decimal)mn : null;
            decimal? mx = p["maximum"] is JsonValue mx2 ? (decimal)mx2 : null;
            if (min is not null && mx is not null && (number < min || number > mx))
            {
                problems.Add(new JsonObject { ["error"] = "range", ["message"] = $"This field must be between {min} and {mx}", ["minimum"] = min, ["maximum"] = mx });
            }
            else if (min is not null && number < min)
            {
                problems.Add(new JsonObject { ["error"] = "minimum", ["message"] = $"This field must be at least {min}", ["minimum"] = min });
            }
            else if (mx is not null && number > mx)
            {
                problems.Add(new JsonObject { ["error"] = "maximum", ["message"] = $"This field must be at most {mx}", ["maximum"] = mx });
            }
        }
        _ = path;
        return problems.Count > 0 ? problems : null;
    }
}
