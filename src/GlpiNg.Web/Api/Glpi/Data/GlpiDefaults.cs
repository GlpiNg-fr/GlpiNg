using System.Globalization;

namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>
/// Valeur par défaut d'une colonne GLPI que GlpiNg n'alimente pas : celle que prendrait la colonne
/// dans une base GLPI où elle n'a jamais été renseignée (clause DEFAULT de glpi-empty.sql).
/// </summary>
public static class GlpiDefaults
{
    private static readonly Dictionary<string, Dictionary<string, object?>> Cache = new(StringComparer.Ordinal);

    public static object? DefaultValue(string table, string column)
    {
        Dictionary<string, object?> defaults;
        lock (Cache)
        {
            if (!Cache.TryGetValue(table, out defaults!))
            {
                defaults = GlpiMetadata.Instance.ColumnsOf(table).ToDictionary(c => c.Name, Compute, StringComparer.Ordinal);
                Cache[table] = defaults;
            }
        }
        return defaults.GetValueOrDefault(column);
    }

    private static object? Compute(GlpiColumn column)
    {
        string? raw = column.Default;
        if (column.HasDefault && raw is not null && !raw.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase))
        {
            if (column.IsInteger && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
            {
                return l;
            }
            if ((column.IsDecimal || column.IsFloat) && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d))
            {
                return d;
            }
            return raw;
        }

        if (column.HasDefault || column.Nullable)
        {
            return null;
        }

        if (column.IsInteger)
        {
            return 0L;
        }
        if (column.IsDecimal || column.IsFloat)
        {
            return 0m;
        }
        return column.IsDateTime || column.IsDate || column.IsTime ? null : string.Empty;
    }
}
