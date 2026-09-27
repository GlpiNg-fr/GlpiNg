using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GlpiNg.Web.Api.Glpi;
using GlpiNg.Web.Api.Glpi.Data;

namespace GlpiNg.Web.Api.Legacy;

/// <summary>Erreur d'API v1 : code GLPI, message, statut HTTP, et renvoi vers la documentation.</summary>
public sealed class LegacyApiException(string statusCode, string message, int httpCode = 400, bool docMessage = true)
    : Exception(message)
{
    public string StatusCode { get; } = statusCode;
    public int HttpCode { get; } = httpCode;
    public bool DocMessage { get; } = docMessage;

    /// <summary>Corps d'erreur déjà construit (collection de résultats d'un ajout multiple...).</summary>
    public JsonNode? Payload { get; init; }
}

/// <summary>
/// API::searchItems : moteur de recherche de GLPI (critères par option de recherche numérotée,
/// colonnes affichées, tri, pagination), évalué sur les tables virtuelles.
/// </summary>
public sealed partial class LegacySearch(GlpiDataContext ctx, int listLimit, bool multiEntities)
{
    private static readonly string[] AllAssetTypes =
        ["Computer", "Monitor", "NetworkEquipment", "Peripheral", "Phone", "Printer", "SoftwareLicense", "Certificate", "Unmanaged", "Appliance"];

    public async Task<(JsonObject Body, int TotalCount, int Count, string ContentRange)> SearchAsync(GlpiItemtypeInfo itemtype, JsonObject p, CancellationToken ct)
    {
        JsonObject soptions = itemtype.SearchOptions;

        // Validation des critères, avec les messages de GLPI.
        JsonArray criteria = AsList(p["criteria"]);
        string? invalid = Check(criteria, soptions);
        if (invalid is not null)
        {
            throw new LegacyApiException("ERROR", invalid);
        }

        // forcedisplay
        List<int> forcedisplay = [];
        if (p["forcedisplay"] is JsonNode fd)
        {
            foreach (JsonNode? n in fd is JsonArray or JsonObject ? AsList(fd) : [fd])
            {
                if (GlpiValue.TryToLong(GlpiValue.FromJson(n), out long f))
                {
                    forcedisplay.Add((int)f);
                }
            }
            foreach (int f in forcedisplay)
            {
                if (soptions[f.ToString(CultureInfo.InvariantCulture)] is JsonObject o && o["nodisplay"]?.GetValue<bool>() == true)
                {
                    throw new LegacyApiException("ERROR", "ID is forbidden along with 'forcedisplay' parameter.");
                }
            }
        }

        (int start, int end) = ParseRange(p["range"], listLimit);

        // sort / order
        List<int> sort = [];
        foreach (JsonNode? s in p["sort"] is JsonNode sortNode ? (sortNode is JsonArray or JsonObject ? AsList(sortNode) : [sortNode]) : [])
        {
            if (GlpiValue.TryToLong(GlpiValue.FromJson(s), out long l) && l >= 0)
            {
                sort.Add((int)l);
            }
        }
        List<string> order = [];
        foreach (JsonNode? o in p["order"] is JsonNode orderNode ? (orderNode is JsonArray or JsonObject ? AsList(orderNode) : [orderNode]) : [])
        {
            string value = o?.ToString().ToUpperInvariant() ?? "ASC";
            order.Add(value == "DESC" ? "DESC" : "ASC");
        }

        // Colonnes affichées (SearchEngine::prepareDataForSearch).
        List<int> toview = [.. itemtype.DefaultToView];
        if (multiEntities && itemtype.EntityAssign && itemtype.EntityOption > 0 && !toview.Contains(itemtype.EntityOption))
        {
            toview.Add(itemtype.EntityOption);
        }
        if (sort.Count == 0 || sort.SequenceEqual([0]))
        {
            sort = [toview.Count > 0 ? toview[0] : 1];
        }
        if (forcedisplay.Count > 0)
        {
            toview.AddRange(forcedisplay);
        }
        else if (GlpiMetadata.Instance.Defaults["displaypreferences"]?[itemtype.Name] is JsonArray prefs)
        {
            toview.AddRange(prefs.Select(x => (int)x!));
        }
        CollectCriteriaFields(criteria, toview);
        toview.AddRange(sort.Except(toview));
        if (itemtype.IsItil && itemtype.IdOption > 0)
        {
            toview.Insert(0, itemtype.IdOption);
        }
        List<int> cols = [.. toview.Distinct().Where(id => soptions.ContainsKey(id.ToString(CultureInfo.InvariantCulture)))];

        bool isDeleted = p["is_deleted"] is JsonNode del && GlpiValue.TryToBool(GlpiValue.FromJson(del), out bool d) && d;

        List<(string Itemtype, int Id, Dictionary<int, JsonNode?> Values)> rows;
        int total;
        if (itemtype.Name == "AllAssets")
        {
            (rows, total) = await SearchAllAssetsAsync(criteria, cols, sort, order, isDeleted, start, end, ct);
        }
        else
        {
            (rows, total) = await SearchTypeAsync(itemtype, criteria, cols, sort, order, isDeleted, start, end, ct);
        }

        if (start > total)
        {
            throw new LegacyApiException("ERROR_RANGE_EXCEED_TOTAL", "Provided range exceed total count of data: " + total);
        }

        JsonObject body = new()
        {
            ["totalcount"] = total,
            ["count"] = rows.Count,
            ["sort"] = new JsonArray([.. sort.Select(s => (JsonNode)s)]),
            ["order"] = new JsonArray([.. (order.Count > 0 ? order : ["ASC"]).Select(o => (JsonNode)o)]),
        };

        bool withIndexes = p.ContainsKey("withindexes") && Truthy(p["withindexes"]);
        bool uidCols = p.ContainsKey("uid_cols") && Truthy(p["uid_cols"]);
        JsonObject indexed = [];
        JsonArray list = [];
        foreach ((string rowItemtype, int id, Dictionary<int, JsonNode?> values) in rows)
        {
            JsonObject line = [];
            foreach (int col in cols)
            {
                string key = uidCols
                    ? (string?)soptions[col.ToString(CultureInfo.InvariantCulture)]?["uid"] ?? col.ToString(CultureInfo.InvariantCulture)
                    : col.ToString(CultureInfo.InvariantCulture);
                line[key] = values.GetValueOrDefault(col)?.DeepClone();
            }
            if (itemtype.Name == "AllAssets")
            {
                line["id"] = id;
                line["itemtype"] = rowItemtype;
            }
            if (withIndexes)
            {
                indexed[id.ToString(CultureInfo.InvariantCulture)] = line;
            }
            else
            {
                list.Add(line);
            }
        }
        if (rows.Count > 0)
        {
            body["data"] = withIndexes ? indexed : list;
        }

        if (p.ContainsKey("giveItems") && Truthy(p["giveItems"]))
        {
            body["data_html"] = body["data"]?.DeepClone() ?? new JsonArray();
        }

        int fixedEnd = Math.Min(end, total - 1);
        string range = $"{start}-{fixedEnd}/{total}";
        body["content-range"] = range;
        return (body, total, rows.Count, range);
    }

    private async Task<(List<(string, int, Dictionary<int, JsonNode?>)>, int)> SearchTypeAsync(GlpiItemtypeInfo itemtype, JsonArray criteria,
        List<int> cols, List<int> sort, List<string> order, bool isDeleted, int start, int end, CancellationToken ct)
    {
        string table = itemtype.Table!;
        GlpiFilter baseFilter = BaseFilter(itemtype, isDeleted);
        IGlpiTable main = ctx.Table(table);

        HashSet<int>? matching = criteria.Count == 0 ? null : await EvaluateAsync(itemtype, criteria, baseFilter, ct);

        List<int> page;
        int total;
        GlpiSearchOption? sortOption = itemtype.RawSearchOptions.GetValueOrDefault(sort[0]);
        GlpiSearchPath? sortPath = sortOption is null ? null : GlpiSearchPath.For(itemtype, sortOption);
        bool descending = order.Count > 0 && order[0] == "DESC";

        if (matching is null && (sortPath is null || sortPath.IsDirect))
        {
            // Pas de critère, tri sur une colonne de la table : tout est confié à la base.
            List<GlpiSort>? sorts = sortPath is null ? null : [new GlpiSort(sortPath.Field, descending)];
            GlpiPage result = await main.QueryAsync(ctx, baseFilter, sorts, start, end - start + 1, ct);
            (page, total) = ([.. result.Ids], result.Total);
        }
        else
        {
            List<int> ids = matching is null ? [.. (await main.QueryAsync(ctx, baseFilter, null, 0, null, ct)).Ids] : [.. matching];
            total = ids.Count;
            if (sortPath is not null)
            {
                Dictionary<int, List<object?>> keys = await GlpiSearchEvaluator.ValuesAsync(ctx, table, sortPath, ids, ct);
                IComparer<object?> comparer = Comparer<object?>.Create(GlpiValue.Compare);
                ids = descending
                    ? [.. ids.OrderByDescending(id => keys[id].FirstOrDefault(), comparer).ThenBy(id => id)]
                    : [.. ids.OrderBy(id => keys[id].FirstOrDefault(), comparer).ThenBy(id => id)];
            }
            else
            {
                ids.Sort();
            }
            page = [.. ids.Skip(start).Take(end - start + 1)];
        }

        Dictionary<int, Dictionary<int, JsonNode?>> values = page.ToDictionary(id => id, _ => new Dictionary<int, JsonNode?>());
        foreach (int col in cols)
        {
            if (!itemtype.RawSearchOptions.TryGetValue(col, out GlpiSearchOption? option))
            {
                continue;
            }
            GlpiSearchPath path = GlpiSearchPath.For(itemtype, option);
            Dictionary<int, List<object?>> colValues = await GlpiSearchEvaluator.ValuesAsync(ctx, table, path, page, ct);
            GlpiColumn? column = GlpiMetadata.Instance.ColumnsOf(path.Table).FirstOrDefault(c => c.Name == path.Field);
            foreach (int id in page)
            {
                List<object?> v = colValues[id];
                values[id][col] = v.Count switch
                {
                    0 => null,
                    1 => GlpiNames.Format(column, v[0]),
                    _ => new JsonArray([.. v.Select(x => GlpiNames.Format(column, x))]),
                };
            }
        }

        return ([.. page.Select(id => (itemtype.Name, id, values[id]))], total);
    }

    private async Task<(List<(string, int, Dictionary<int, JsonNode?>)>, int)> SearchAllAssetsAsync(JsonArray criteria,
        List<int> cols, List<int> sort, List<string> order, bool isDeleted, int start, int end, CancellationToken ct)
    {
        // AllAssets : la même recherche sur chaque type d'actif, résultats mis bout à bout.
        List<(string, int, Dictionary<int, JsonNode?>)> all = [];
        foreach (string type in AllAssetTypes)
        {
            if (GlpiMetadata.Instance.FindItemtype(type) is not { Table: not null } info || !ctx.Catalog.IsBacked(info.Table))
            {
                continue;
            }
            (List<(string, int, Dictionary<int, JsonNode?>)> rows, _) =
                await SearchTypeAsync(info, criteria, [.. cols.Where(info.RawSearchOptions.ContainsKey)], sort, order, isDeleted, 0, int.MaxValue - 1, ct);
            all.AddRange(rows);
        }
        return ([.. all.Skip(start).Take(end - start + 1)], all.Count);
    }

    private static GlpiFilter BaseFilter(GlpiItemtypeInfo itemtype, bool isDeleted)
    {
        List<GlpiFilter> filters = [];
        if (itemtype.MaybeDeleted)
        {
            filters.Add(GlpiFilter.Col("is_deleted", GlpiOp.Eq, isDeleted ? 1L : 0L));
        }
        if (itemtype.MaybeTemplate)
        {
            filters.Add(GlpiFilter.Col("is_template", GlpiOp.Eq, 0L));
        }
        return GlpiFilter.And([.. filters]);
    }

    /// <summary>Critères combinés comme le SQL de GLPI : ET prioritaire sur OU, « AND NOT » = ET NON.</summary>
    private async Task<HashSet<int>> EvaluateAsync(GlpiItemtypeInfo itemtype, JsonArray criteria, GlpiFilter baseFilter, CancellationToken ct)
    {
        HashSet<int>? universe = null;
        async Task<HashSet<int>> Universe() => universe ??= [.. (await ctx.Table(itemtype.Table!).QueryAsync(ctx, baseFilter, null, 0, null, ct)).Ids];

        List<HashSet<int>> orGroups = [];
        HashSet<int>? current = null;
        bool first = true;

        foreach (JsonNode? node in criteria)
        {
            if (node is not JsonObject criterion)
            {
                continue;
            }
            string link = first ? "AND" : ((string?)criterion["link"] ?? "AND").ToUpperInvariant().Trim();
            first = false;

            HashSet<int> set;
            if (criterion["criteria"] is JsonNode nested)
            {
                set = await EvaluateAsync(itemtype, AsList(nested), baseFilter, ct);
            }
            else if (criterion["meta"] is JsonNode meta && Truthy(meta))
            {
                // ponytail: critères « meta » (liaison vers un autre itemtype) non évalués — ils ne
                // retiennent rien, plutôt que d'élargir le résultat en les ignorant.
                set = [];
            }
            else
            {
                set = await CriterionAsync(itemtype, criterion, baseFilter, Universe, ct);
            }

            bool negate = link.EndsWith("NOT", StringComparison.Ordinal);
            if (negate)
            {
                set = [.. (await Universe()).Except(set)];
            }

            if (link.StartsWith("OR", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    orGroups.Add(current);
                }
                current = set;
            }
            else
            {
                current = current is null ? set : [.. current.Intersect(set)];
            }
        }
        if (current is not null)
        {
            orGroups.Add(current);
        }
        HashSet<int> result = [];
        foreach (HashSet<int> group in orGroups)
        {
            result.UnionWith(group);
        }
        return result;
    }

    private async Task<HashSet<int>> CriterionAsync(GlpiItemtypeInfo itemtype, JsonObject criterion, GlpiFilter baseFilter,
        Func<Task<HashSet<int>>> universe, CancellationToken ct)
    {
        int field = (int)GlpiValue.FromJson(criterion["field"]).ToLong();
        string searchtype = ((string?)criterion["searchtype"] ?? "contains").ToLowerInvariant();
        object? value = GlpiValue.FromJson(criterion["value"]);
        string text = GlpiValue.ToText(value) ?? string.Empty;

        if (!itemtype.RawSearchOptions.TryGetValue(field, out GlpiSearchOption? option))
        {
            return [];
        }
        GlpiSearchPath path = GlpiSearchPath.For(itemtype, option);
        string table = itemtype.Table!;

        // « equals » sur une liste déroulante porte sur l'identifiant, pas sur le texte.
        bool byId = option.Datatype is "dropdown" or "itemlink" && !option.SearchEqualsOnField;
        bool numeric = option.Datatype is "number" or "integer" or "count" or "decimal" or "actiontime" or "timestamp";

        async Task<HashSet<int>> Match(GlpiFilter targetFilter) => await GlpiSearchEvaluator.MatchingAsync(ctx, table, path, targetFilter, baseFilter, ct);

        switch (searchtype)
        {
            case "contains":
            case "notcontains":
            {
                HashSet<int> set;
                if (text.Length == 0)
                {
                    set = await universe();
                }
                else if (numeric && NumericOperator().Match(text) is { Success: true } m)
                {
                    GlpiOp op = m.Groups[1].Value switch { "<" => GlpiOp.Lt, "<=" => GlpiOp.Le, ">" => GlpiOp.Gt, ">=" => GlpiOp.Ge, _ => GlpiOp.Eq };
                    set = await Match(GlpiFilter.Col(path.Field, op, m.Groups[2].Value));
                }
                else if (text.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                {
                    set = await EmptyAsync(path, table, baseFilter, universe, ct);
                }
                else
                {
                    set = await Match(GlpiFilter.Col(path.Field, GlpiOp.Like, GlpiValue.MakeTextSearchPattern(text)));
                }
                return searchtype == "contains" ? set : [.. (await universe()).Except(set)];
            }
            case "equals":
            case "notequals":
            {
                GlpiFilter filter = byId && !path.IsDirect
                    ? GlpiFilter.Col("id", GlpiOp.Eq, value)
                    : byId && option.Datatype == "itemlink" ? GlpiFilter.Col("id", GlpiOp.Eq, value) : GlpiFilter.Col(path.Field, GlpiOp.Eq, value);
                HashSet<int> set = await Match(filter);
                return searchtype == "equals" ? set : [.. (await universe()).Except(set)];
            }
            case "lessthan":
                return await Match(GlpiFilter.Col(path.Field, GlpiOp.Lt, value));
            case "morethan":
                return await Match(GlpiFilter.Col(path.Field, GlpiOp.Gt, value));
            case "under":
            case "notunder":
            {
                List<object?> ids = [.. (await DescendantsAsync(path.Table, (int)value.ToLong(), ct)).Cast<object?>()];
                HashSet<int> set = await Match(GlpiFilter.Col("id", GlpiOp.In, ids));
                return searchtype == "under" ? set : [.. (await universe()).Except(set)];
            }
            case "empty":
                return await EmptyAsync(path, table, baseFilter, universe, ct);
            default:
                return [];
        }
    }

    /// <summary>Sans valeur : aucune ligne jointe, ou une valeur nulle, vide ou nulle (0).</summary>
    private async Task<HashSet<int>> EmptyAsync(GlpiSearchPath path, string table, GlpiFilter baseFilter, Func<Task<HashSet<int>>> universe, CancellationToken ct)
    {
        HashSet<int> all = await universe();
        Dictionary<int, List<object?>> values = await GlpiSearchEvaluator.ValuesAsync(ctx, table, path, all, ct);
        return [.. all.Where(id => values[id].Count == 0 || values[id].All(v => v is null || (v is string s && s.Length == 0)))];
    }

    /// <summary>Identifiants d'une valeur d'arborescence et de ses descendants.</summary>
    private async Task<HashSet<int>> DescendantsAsync(string table, int id, CancellationToken ct)
    {
        string parentColumn = GlpiSearchPath.ForeignKeyForTable(table);
        Dictionary<int, object?> parents = await ctx.Table(table).GetColumnAsync(ctx, parentColumn, null, ct);
        HashSet<int> result = [id];
        bool added = true;
        while (added)
        {
            added = false;
            foreach ((int child, object? parent) in parents)
            {
                if (GlpiValue.TryToLong(parent, out long p) && result.Contains((int)p) && result.Add(child))
                {
                    added = true;
                }
            }
        }
        return result;
    }

    [GeneratedRegex(@"^\s*(<=|>=|<|>)\s*(-?[\d.]+)\s*$")]
    private static partial Regex NumericOperator();

    private static void CollectCriteriaFields(JsonArray criteria, List<int> toview)
    {
        foreach (JsonObject criterion in criteria.OfType<JsonObject>())
        {
            if (criterion["criteria"] is JsonNode nested)
            {
                CollectCriteriaFields(AsList(nested), toview);
            }
            else if (!(criterion["meta"] is JsonNode meta && Truthy(meta))
                     && GlpiValue.TryToLong(GlpiValue.FromJson(criterion["field"]), out long f) && !toview.Contains((int)f))
            {
                toview.Add((int)f);
            }
        }
    }

    private static string? Check(JsonArray criteria, JsonObject soptions)
    {
        foreach (JsonObject criterion in criteria.OfType<JsonObject>())
        {
            if (criterion["criteria"] is JsonNode nested)
            {
                return Check(AsList(nested), soptions);
            }
            if (criterion["field"] is null || criterion["searchtype"] is null || !criterion.ContainsKey("value"))
            {
                return "Malformed search criteria";
            }
            string field = criterion["field"]!.ToString();
            if (!field.All(char.IsDigit) || !soptions.ContainsKey(field))
            {
                return "Bad field ID in search criteria";
            }
            if (soptions[field]?["nosearch"]?.GetValue<bool>() == true)
            {
                return "Forbidden field ID in search criteria";
            }
        }
        return null;
    }

    /// <summary>« range » au format GLPI (début-fin), par défaut la taille de page de l'utilisateur.</summary>
    public static (int Start, int End) ParseRange(JsonNode? range, int listLimit)
    {
        if (range is null)
        {
            return (0, listLimit - 1);
        }
        Match m = Regex.Match(range.ToString(), "^([0-9]+)-([0-9]+)$");
        if (!m.Success)
        {
            throw new LegacyApiException("ERROR", "range must be in format : [start-end] with integers");
        }
        return (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Tableau PHP (liste ou tableau à clés numériques) vu comme une liste ordonnée.</summary>
    public static JsonArray AsList(JsonNode? node) => node switch
    {
        JsonArray a => a,
        JsonObject o => new JsonArray([.. o.OrderBy(kv => int.TryParse(kv.Key, out int k) ? k : int.MaxValue).Select(kv => kv.Value?.DeepClone())]),
        _ => [],
    };

    public static bool Truthy(JsonNode? node) => GlpiValue.TryToBool(GlpiValue.FromJson(node), out bool b) ? b : node is not null;
}

internal static class LegacyValueExtensions
{
    public static long ToLong(this object? value) => GlpiValue.TryToLong(value, out long l) ? l : 0;
}
