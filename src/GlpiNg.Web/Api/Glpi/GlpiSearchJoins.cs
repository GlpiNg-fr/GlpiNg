using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Web.Api.Glpi.Data;

namespace GlpiNg.Web.Api.Glpi;

/// <summary>Étape d'une jointure de recherche, d'une table vers la suivante.</summary>
/// <param name="Table">Table atteinte.</param>
/// <param name="ReferenceColumn">
/// Clé étrangère portée par la table de départ vers <see cref="Table"/>.id (jointure « standard »),
/// ou null pour une jointure « enfant ».
/// </param>
/// <param name="ChildColumn">Colonne de <see cref="Table"/> désignant la ligne de départ (jointure « enfant »).</param>
/// <param name="Conditions">Conditions supplémentaires sur <see cref="Table"/> (itemtype = ..., is_deleted = 0...).</param>
public sealed record GlpiJoinStep(string Table, string? ReferenceColumn, string? ChildColumn, GlpiFilter? Conditions);

/// <summary>
/// Chemin d'une option de recherche GLPI : jointures depuis la table de l'itemtype jusqu'à la table
/// de l'option, puis champ lu. Reproduit Search::addLeftJoin pour les types de jointure courants
/// (standard, child, itemtype_item, mainitemtype_mainitem, itemtypeonly, beforejoin) ; les autres
/// rendent l'option inévaluable (<see cref="IsSupported"/> faux).
/// </summary>
public sealed class GlpiSearchPath
{
    public required IReadOnlyList<GlpiJoinStep> Steps { get; init; }

    public required string Table { get; init; }

    public required string Field { get; init; }

    public bool IsSupported { get; init; } = true;

    /// <summary>Vrai si l'option lit directement une colonne de la table de l'itemtype.</summary>
    public bool IsDirect => Steps.Count == 0;

    public static GlpiSearchPath For(GlpiItemtypeInfo itemtype, GlpiSearchOption option)
    {
        string main = itemtype.Table!;
        string? linkfield = option.LinkField;

        if (option.Table == main && (linkfield is null || linkfield == option.Field) && option.JoinParams is not { Count: > 0 })
        {
            return new GlpiSearchPath { Steps = [], Table = main, Field = option.Field };
        }

        List<GlpiJoinStep> steps = [];
        bool supported = true;
        string current = main;
        string currentItemtype = itemtype.Name;

        // beforejoin : jointures intermédiaires, de la plus profonde à la plus proche de l'option.
        List<JsonObject> before = [];
        JsonNode? node = option.JoinParams?["beforejoin"];
        while (node is JsonObject bj)
        {
            before.Insert(0, bj);
            node = bj["joinparams"]?["beforejoin"];
        }
        // Un beforejoin peut aussi être une liste de jointures.
        if (option.JoinParams?["beforejoin"] is JsonArray list)
        {
            before = [.. list.OfType<JsonObject>()];
        }

        foreach (JsonObject bj in before)
        {
            string? table = (string?)bj["table"];
            if (table is null)
            {
                supported = false;
                break;
            }
            GlpiJoinStep? step = Step(current, currentItemtype, table, (string?)bj["linkfield"], bj["joinparams"] as JsonObject, ref supported);
            if (step is not null)
            {
                steps.Add(step);
            }
            current = table;
            currentItemtype = GlpiMetadata.Instance.ItemtypeForTable(table) ?? currentItemtype;
        }

        JsonObject? ownParams = option.JoinParams is null ? null : StripBeforeJoin(option.JoinParams);
        GlpiJoinStep? last = Step(current, currentItemtype, option.Table, linkfield, ownParams, ref supported);
        if (last is not null)
        {
            steps.Add(last);
        }

        return new GlpiSearchPath { Steps = steps, Table = option.Table, Field = option.Field, IsSupported = supported && !option.HasComputation };
    }

    private static JsonObject StripBeforeJoin(JsonObject joinParams)
    {
        JsonObject copy = joinParams.DeepClone().AsObject();
        copy.Remove("beforejoin");
        return copy;
    }

    private static GlpiJoinStep? Step(string fromTable, string fromItemtype, string toTable, string? linkfield, JsonObject? joinParams, ref bool supported)
    {
        string jointype = (string?)joinParams?["jointype"] ?? string.Empty;
        GlpiFilter? conditions = Conditions(joinParams?["condition"]);
        string itemtypeValue = (string?)joinParams?["specific_itemtype"] ?? fromItemtype;

        switch (jointype)
        {
            case "":
            {
                string fk = !string.IsNullOrEmpty(linkfield) ? linkfield : ForeignKeyForTable(toTable);
                return new GlpiJoinStep(toTable, fk, null, conditions);
            }
            case "child":
            {
                string fk = (string?)joinParams?["linkfield"] ?? ForeignKeyForTable(fromTable);
                return new GlpiJoinStep(toTable, null, fk, conditions);
            }
            case "itemtype_item":
            {
                string idColumn = (string?)joinParams?["specific_items_id_column"] ?? "items_id";
                string typeColumn = (string?)joinParams?["specific_itemtype_column"] ?? "itemtype";
                return new GlpiJoinStep(toTable, null, idColumn,
                    GlpiFilter.And(GlpiFilter.Col(typeColumn, GlpiOp.Eq, itemtypeValue), conditions));
            }
            case "mainitemtype_mainitem":
                return new GlpiJoinStep(toTable, null, "mainitems_id",
                    GlpiFilter.And(GlpiFilter.Col("mainitemtype", GlpiOp.Eq, itemtypeValue), conditions));
            case "itemtypeonly":
                // Toutes les lignes du type, sans lien avec l'élément : GLPI s'en sert rarement.
                supported = false;
                return null;
            case "empty":
                return null;
            default:
                supported = false;
                return null;
        }
    }

    /// <summary>getForeignKeyFieldForTable de GLPI : glpi_computers → computers_id.</summary>
    public static string ForeignKeyForTable(string table) => (table.StartsWith("glpi_", StringComparison.Ordinal) ? table[5..] : table) + "_id";

    /// <summary>Conditions simples de joinparams (« NEWTABLE.col » =&gt; valeur) ; les formes complexes sont ignorées.</summary>
    private static GlpiFilter? Conditions(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return null;
        }
        List<GlpiFilter> filters = [];
        foreach ((string key, JsonNode? value) in obj)
        {
            string column = key.StartsWith("NEWTABLE.", StringComparison.Ordinal) ? key[9..] : key;
            if (column.Contains('.') || column is "NOT" or "OR" or "AND")
            {
                continue;
            }
            if (value is JsonValue v && v.GetValueKind() is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                filters.Add(GlpiFilter.Col(column, GlpiOp.Eq, GlpiValue.FromJson(v)));
            }
        }
        return filters.Count == 0 ? null : GlpiFilter.And([.. filters]);
    }
}

/// <summary>Évaluation des chemins de recherche sur les tables virtuelles.</summary>
public static class GlpiSearchEvaluator
{
    /// <summary>Valeurs de l'option pour chaque élément principal (liste : une option « enfant » en porte plusieurs).</summary>
    public static async Task<Dictionary<int, List<object?>>> ValuesAsync(GlpiDataContext ctx, string mainTable, GlpiSearchPath path,
        IReadOnlyCollection<int> mainIds, CancellationToken ct)
    {
        Dictionary<int, List<object?>> result = mainIds.ToDictionary(id => id, _ => new List<object?>());
        if (mainIds.Count == 0 || !path.IsSupported)
        {
            return result;
        }

        Dictionary<int, List<int>> reach = await ReachAsync(ctx, mainTable, path.Steps, mainIds, ct);

        HashSet<int> finalIds = [.. reach.Values.SelectMany(v => v)];
        Dictionary<int, object?> values = finalIds.Count == 0
            ? []
            : await ctx.Table(path.Table).GetColumnAsync(ctx, path.Field, GlpiFilter.IdIn(finalIds), ct);
        foreach ((int main, List<int> rows) in reach)
        {
            result[main] = [.. rows.Where(values.ContainsKey).Select(r => values[r])];
        }
        return result;
    }

    /// <summary>Lignes de la dernière table du chemin atteintes depuis chaque élément principal.</summary>
    public static async Task<Dictionary<int, List<int>>> ReachAsync(GlpiDataContext ctx, string mainTable, IReadOnlyList<GlpiJoinStep> steps,
        IReadOnlyCollection<int> mainIds, CancellationToken ct)
    {
        // Correspondance élément principal → lignes de la table courante.
        Dictionary<int, List<int>> reach = mainIds.ToDictionary(id => id, id => new List<int> { id });
        string current = mainTable;

        foreach (GlpiJoinStep step in steps)
        {
            HashSet<int> currentIds = [.. reach.Values.SelectMany(v => v)];
            Dictionary<int, List<int>> next = [];
            if (step.ReferenceColumn is not null)
            {
                Dictionary<int, object?> refs = await ctx.Table(current).GetColumnAsync(ctx, step.ReferenceColumn, GlpiFilter.IdIn(currentIds), ct);
                HashSet<int> targets = [.. refs.Values.Select(ToId).Where(id => id is not null).Select(id => id!.Value)];
                if (step.Conditions is not null && targets.Count > 0)
                {
                    targets = [.. (await ctx.Table(step.Table).QueryAsync(ctx, GlpiFilter.And(GlpiFilter.IdIn(targets), step.Conditions), null, 0, null, ct)).Ids];
                }
                foreach ((int main, List<int> rows) in reach)
                {
                    next[main] = [.. rows.Select(r => ToId(refs.GetValueOrDefault(r))).Where(t => t is not null && targets.Contains(t.Value)).Select(t => t!.Value)];
                }
            }
            else
            {
                GlpiFilter filter = GlpiFilter.And(GlpiFilter.Col(step.ChildColumn!, GlpiOp.In, currentIds.Cast<object?>().ToList()), step.Conditions);
                Dictionary<int, object?> parents = await ctx.Table(step.Table).GetColumnAsync(ctx, step.ChildColumn!, filter, ct);
                ILookup<int, int> byParent = parents.Where(p => ToId(p.Value) is not null).ToLookup(p => ToId(p.Value)!.Value, p => p.Key);
                foreach ((int main, List<int> rows) in reach)
                {
                    next[main] = [.. rows.SelectMany(r => byParent[r]).OrderBy(id => id)];
                }
            }
            reach = next;
            current = step.Table;
        }
        return reach;
    }

    /// <summary>
    /// Éléments principaux dont une ligne de la table de l'option vérifie <paramref name="targetFilter"/>
    /// (condition exprimée sur la table de l'option), en remontant le chemin de jointure.
    /// </summary>
    public static async Task<HashSet<int>> MatchingAsync(GlpiDataContext ctx, string mainTable, GlpiSearchPath path,
        GlpiFilter targetFilter, GlpiFilter mainFilter, CancellationToken ct)
    {
        if (!path.IsSupported)
        {
            return [];
        }
        if (path.IsDirect)
        {
            return [.. (await ctx.Table(mainTable).QueryAsync(ctx, GlpiFilter.And(mainFilter, targetFilter), null, 0, null, ct)).Ids];
        }

        List<string> tables = [mainTable, .. path.Steps.Select(s => s.Table)];
        HashSet<int> ids = [.. (await ctx.Table(path.Table).QueryAsync(ctx, GlpiFilter.And(targetFilter, path.Steps[^1].Conditions), null, 0, null, ct)).Ids];

        for (int i = path.Steps.Count - 1; i >= 0 && ids.Count > 0; i--)
        {
            GlpiJoinStep step = path.Steps[i];
            string from = tables[i];
            GlpiFilter? fromFilter = i == 0 ? mainFilter : path.Steps[i - 1].Conditions;
            if (step.ReferenceColumn is not null)
            {
                GlpiFilter byRef = GlpiFilter.Col(step.ReferenceColumn, GlpiOp.In, ids.Cast<object?>().ToList());
                ids = [.. (await ctx.Table(from).QueryAsync(ctx, GlpiFilter.And(byRef, fromFilter), null, 0, null, ct)).Ids];
            }
            else
            {
                Dictionary<int, object?> parents = await ctx.Table(step.Table).GetColumnAsync(ctx, step.ChildColumn!, GlpiFilter.IdIn(ids), ct);
                HashSet<int> parentIds = [.. parents.Values.Select(ToId).Where(p => p is not null).Select(p => p!.Value)];
                ids = parentIds.Count == 0
                    ? []
                    : [.. (await ctx.Table(from).QueryAsync(ctx, GlpiFilter.And(GlpiFilter.IdIn(parentIds), fromFilter), null, 0, null, ct)).Ids];
            }
        }
        return ids;
    }

    private static int? ToId(object? value) => GlpiValue.TryToLong(value, out long l) && l >= 0 ? (int)l : null;
}
