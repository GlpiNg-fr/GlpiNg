using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>
/// Conversions entre les valeurs telles que GLPI les échange (nombres, 0/1, textes
/// « 2024-01-31 12:00:00 »...) et les types CLR du modèle GlpiNg, et comparaison en mémoire.
/// </summary>
public static class GlpiValue
{
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly string[] AcceptedDateFormats =
    [
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-ddTHH:mm",
    ];

    /// <summary>Valeur CLR portée par un nœud JSON d'entrée (nombre, texte, booléen, null).</summary>
    public static object? FromJson(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            switch (value.GetValueKind())
            {
                case JsonValueKind.String:
                    return value.GetValue<string>();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    // Un JsonValue peut être adossé au type .NET d'origine (int, double...) plutôt
                    // qu'au texte JSON : TryGetValue ne convertit alors que vers ce type exact, et
                    // GetValue<double> lève sur un JsonValue<int>. On essaie donc chaque type, puis
                    // on relit le texte JSON, qui marche quelle que soit la provenance du nœud.
                    if (value.TryGetValue(out long l))
                    {
                        return l;
                    }
                    if (value.TryGetValue(out int i))
                    {
                        return (long)i;
                    }
                    if (value.TryGetValue(out decimal d))
                    {
                        return d;
                    }
                    if (value.TryGetValue(out double dbl))
                    {
                        return dbl;
                    }
                    string text = value.ToJsonString();
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedLong))
                    {
                        return parsedLong;
                    }
                    return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedDecimal)
                        ? parsedDecimal
                        : 0L;
                case JsonValueKind.Null:
                    return null;
            }
        }

        // Objet ou tableau : GLPI v2 accepte { "id": 3 } pour une clé étrangère.
        if (node is JsonObject obj && obj["id"] is JsonNode id)
        {
            return FromJson(id);
        }

        return node.ToJsonString();
    }

    public static bool TryToLong(object? value, out long result)
    {
        switch (value)
        {
            case null:
                result = 0;
                return false;
            case long l:
                result = l;
                return true;
            case int i:
                result = i;
                return true;
            case bool b:
                result = b ? 1 : 0;
                return true;
            case decimal d:
                result = (long)d;
                return true;
            case double f:
                result = (long)f;
                return true;
            case Enum e:
                result = Convert.ToInt64(e, CultureInfo.InvariantCulture);
                return true;
            case string s:
                s = s.Trim();
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                {
                    return true;
                }
                if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
                {
                    result = (long)parsed;
                    return true;
                }
                if (bool.TryParse(s, out bool bs))
                {
                    result = bs ? 1 : 0;
                    return true;
                }
                return false;
            default:
                try
                {
                    result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
                {
                    result = 0;
                    return false;
                }
        }
    }

    public static bool TryToDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal d:
                result = d;
                return true;
            case string s:
                return decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
            case double f:
                result = (decimal)f;
                return true;
            default:
                if (TryToLong(value, out long l))
                {
                    result = l;
                    return true;
                }
                result = 0;
                return false;
        }
    }

    public static bool TryToBool(object? value, out bool result)
    {
        switch (value)
        {
            case bool b:
                result = b;
                return true;
            case string s when bool.TryParse(s, out bool parsed):
                result = parsed;
                return true;
            default:
                if (TryToLong(value, out long l))
                {
                    result = l != 0;
                    return true;
                }
                result = false;
                return false;
        }
    }

    public static bool TryToDateTime(object? value, out DateTime result)
    {
        switch (value)
        {
            case DateTime dt:
                result = dt;
                return true;
            case DateTimeOffset dto:
                result = dto.UtcDateTime;
                return true;
            case DateOnly d:
                result = d.ToDateTime(TimeOnly.MinValue);
                return true;
            case string s when !string.IsNullOrWhiteSpace(s):
                s = s.Trim();
                if (DateTime.TryParseExact(s, AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out result))
                {
                    return true;
                }
                if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset any))
                {
                    result = any.Offset == TimeSpan.Zero && !s.EndsWith('Z') && !s.Contains('+') ? any.DateTime : any.UtcDateTime;
                    return true;
                }
                return false;
            default:
                result = default;
                return false;
        }
    }

    /// <summary>
    /// Convertit une valeur GLPI vers <paramref name="target"/> (type d'une propriété du modèle).
    /// Faux si la valeur n'a pas de sens pour ce type (texte non numérique pour un entier...).
    /// </summary>
    public static bool TryConvert(object? value, Type target, out object? result)
    {
        Type? underlying = Nullable.GetUnderlyingType(target);
        bool nullable = underlying is not null || !target.IsValueType;
        Type type = underlying ?? target;

        if (value is null || (value is string empty && empty.Length == 0 && type != typeof(string)))
        {
            result = null;
            return nullable;
        }

        if (type == typeof(string))
        {
            result = ToText(value);
            return true;
        }

        if (type.IsEnum)
        {
            if (TryToLong(value, out long l))
            {
                result = Enum.ToObject(type, l);
                return true;
            }
            if (value is string name && Enum.TryParse(type, name, ignoreCase: true, out object? parsed))
            {
                result = parsed;
                return true;
            }
            result = null;
            return false;
        }

        if (type == typeof(bool))
        {
            bool ok = TryToBool(value, out bool b);
            result = b;
            return ok;
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            if (TryToLong(value, out long l))
            {
                result = Convert.ChangeType(l, type, CultureInfo.InvariantCulture);
                return true;
            }
            result = null;
            return false;
        }

        if (type == typeof(decimal))
        {
            bool ok = TryToDecimal(value, out decimal d);
            result = d;
            return ok;
        }

        if (type == typeof(double) || type == typeof(float))
        {
            bool ok = TryToDecimal(value, out decimal d);
            result = Convert.ChangeType(d, type, CultureInfo.InvariantCulture);
            return ok;
        }

        if (type == typeof(DateTime))
        {
            bool ok = TryToDateTime(value, out DateTime dt);
            result = dt;
            return ok;
        }

        if (type == typeof(DateOnly))
        {
            bool ok = TryToDateTime(value, out DateTime dt);
            result = DateOnly.FromDateTime(dt);
            return ok;
        }

        if (type == typeof(TimeOnly))
        {
            if (value is string ts && TimeOnly.TryParse(ts, CultureInfo.InvariantCulture, out TimeOnly t))
            {
                result = t;
                return true;
            }
            result = null;
            return false;
        }

        result = null;
        return false;
    }

    /// <summary>Représentation textuelle d'une valeur, au format de GLPI.</summary>
    public static string? ToText(object? value) => value switch
    {
        null => null,
        string s => s,
        bool b => b ? "1" : "0",
        DateTime dt => dt.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>Comparaison à la manière de MySQL : numérique si les deux côtés le sont, textuelle insensible à la casse sinon.</summary>
    public static int Compare(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null ? (right is null ? 0 : -1) : 1;
        }

        if (left is DateTime || right is DateTime)
        {
            if (TryToDateTime(left, out DateTime l) && TryToDateTime(right, out DateTime r))
            {
                return l.CompareTo(r);
            }
        }

        if (IsNumeric(left) || IsNumeric(right))
        {
            if (TryToDecimal(left, out decimal l) && TryToDecimal(right, out decimal r))
            {
                return l.CompareTo(r);
            }
        }

        return string.Compare(ToText(left), ToText(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool AreEqual(object? left, object? right) => Compare(left, right) == 0;

    private static bool IsNumeric(object value) => value is long or int or short or byte or decimal or double or float or bool or Enum;

    /// <summary>Motif LIKE (%, _) → expression régulière insensible à la casse.</summary>
    public static Regex LikeToRegex(string pattern)
    {
        System.Text.StringBuilder sb = new("^");
        foreach (char c in pattern)
        {
            sb.Append(c switch
            {
                '%' => ".*",
                '_' => ".",
                _ => Regex.Escape(c.ToString()),
            });
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    public static bool Like(object? value, string pattern)
        => value is not null && LikeToRegex(pattern).IsMatch(ToText(value) ?? string.Empty);

    /// <summary>
    /// Valeur de recherche « contient » de GLPI (SQLProvider::makeTextSearchValue) : joker de part
    /// et d'autre, sauf ancrage explicite par <c>^</c> en tête ou <c>$</c> en fin.
    /// </summary>
    public static string MakeTextSearchPattern(string value)
    {
        value = value.Trim();
        bool anchoredStart = value.StartsWith('^');
        bool anchoredEnd = value.EndsWith('$') && value.Length > (anchoredStart ? 1 : 0);
        if (anchoredStart)
        {
            value = value[1..];
        }
        if (anchoredEnd)
        {
            value = value[..^1];
        }
        // GLPI accepte « * » comme joker dans la saisie.
        value = value.Replace('*', '%');
        return (anchoredStart ? string.Empty : "%") + value + (anchoredEnd ? string.Empty : "%");
    }
}
