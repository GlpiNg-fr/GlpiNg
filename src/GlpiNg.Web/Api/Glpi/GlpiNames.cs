using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GlpiNg.Web.Api.Glpi.Data;

namespace GlpiNg.Web.Api.Glpi;

/// <summary>
/// Conventions de nommage de GLPI : clés étrangères, tables, noms affichés des valeurs référencées
/// (Dropdown::getDropdownName), et mise en forme des valeurs d'une colonne telle que MySQL les rend.
/// </summary>
public static partial class GlpiNames
{
    /// <summary>isForeignKeyField de GLPI : « xxx_id » ou « xxx_id_yyy ».</summary>
    public static bool IsForeignKey(string field)
        => field != "id" && (field.EndsWith("_id", StringComparison.Ordinal) || field.Contains("_id_", StringComparison.Ordinal));

    /// <summary>getTableNameForForeignKeyField de GLPI : users_id_tech → glpi_users.</summary>
    public static string TableForForeignKey(string field) => "glpi_" + ForeignKeySuffix().Replace(field, string.Empty);

    [GeneratedRegex("_id.*$")]
    private static partial Regex ForeignKeySuffix();

    /// <summary>Clé étrangère d'un itemtype (Computer → computers_id).</summary>
    public static string? ForeignKeyForItemtype(string itemtype) => GlpiMetadata.Instance.FindItemtype(itemtype)?.ForeignKey;

    /// <summary>Valeur d'une colonne mise en forme comme GLPI la rend (MySQL, types natifs).</summary>
    public static JsonNode? Format(GlpiColumn? column, object? value)
    {
        if (value is null)
        {
            return null;
        }
        if (column is null)
        {
            return ToJson(value);
        }
        if (column.IsInteger)
        {
            return GlpiValue.TryToLong(value, out long l) ? JsonValue.Create(l) : ToJson(value);
        }
        if (column.IsDecimal)
        {
            return GlpiValue.TryToDecimal(value, out decimal d)
                ? JsonValue.Create(d.ToString("F" + column.Scale, CultureInfo.InvariantCulture))
                : ToJson(value);
        }
        if (column.IsFloat)
        {
            return GlpiValue.TryToDecimal(value, out decimal f) ? JsonValue.Create((double)f) : ToJson(value);
        }
        if (column.IsDateTime)
        {
            return GlpiValue.TryToDateTime(value, out DateTime dt)
                ? JsonValue.Create(dt.ToString(GlpiValue.DateTimeFormat, CultureInfo.InvariantCulture))
                : ToJson(value);
        }
        if (column.IsDate)
        {
            return GlpiValue.TryToDateTime(value, out DateTime date)
                ? JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                : ToJson(value);
        }
        return JsonValue.Create(GlpiValue.ToText(value));
    }

    /// <summary>Valeur sans colonne connue : type JSON naturel.</summary>
    public static JsonNode? ToJson(object? value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b ? 1 : 0),
        long or int or short or byte => JsonValue.Create(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
        decimal d => JsonValue.Create(d),
        double f => JsonValue.Create(f),
        Enum e => JsonValue.Create(Convert.ToInt64(e, CultureInfo.InvariantCulture)),
        JsonNode node => node.DeepClone(),
        _ => JsonValue.Create(GlpiValue.ToText(value)),
    };

    /// <summary>
    /// Nom affiché d'une ligne référencée (Dropdown::getDropdownName) : nom complet pour une
    /// arborescence, « Nom Prénom » pour un utilisateur, désignation pour un composant.
    /// Mémorisé par requête.
    /// </summary>
    public static async Task<string> DropdownNameAsync(GlpiDataContext ctx, string table, int id, CancellationToken ct)
    {
        string key = $"name:{table}:{id}";
        if (ctx.Cache.TryGetValue(key, out object? cached))
        {
            return (string)cached;
        }

        Dictionary<int, GlpiRow> rows = await ctx.Table(table).GetRowsAsync(ctx, [id], ct);
        string name = rows.TryGetValue(id, out GlpiRow? row) ? DisplayName(table, row) : string.Empty;
        ctx.Cache[key] = name;
        return name;
    }

    public static string DisplayName(string table, GlpiRow row)
    {
        if (table == "glpi_users")
        {
            string? realname = GlpiValue.ToText(row.GetValueOrDefault("realname"));
            string? firstname = GlpiValue.ToText(row.GetValueOrDefault("firstname"));
            return string.IsNullOrWhiteSpace(realname)
                ? GlpiValue.ToText(row.GetValueOrDefault("name")) ?? string.Empty
                : $"{realname} {firstname}".Trim();
        }
        foreach (string column in new[] { "completename", "name", "designation" })
        {
            if (GlpiValue.ToText(row.GetValueOrDefault(column)) is { Length: > 0 } text)
            {
                return text;
            }
        }
        return string.Empty;
    }
}
