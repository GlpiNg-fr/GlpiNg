using System.Text.Json.Nodes;

namespace GlpiNg.Web.Api.Legacy;

/// <summary>
/// Lecture de paramètres à la manière de PHP (parse_str) : <c>criteria[0][field]=1</c> devient
/// { "criteria": { "0": { "field": "1" } } }, <c>forcedisplay[]=1&amp;forcedisplay[]=2</c> une liste
/// indexée. Les clients de l'API de GLPI passent leurs tableaux ainsi dans l'URL.
/// </summary>
public static class PhpParams
{
    public static JsonObject Parse(string? query)
    {
        JsonObject root = [];
        if (string.IsNullOrEmpty(query))
        {
            return root;
        }

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string rawKey = Uri.UnescapeDataString((eq < 0 ? pair : pair[..eq]).Replace('+', ' '));
            string value = eq < 0 ? string.Empty : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            Set(root, rawKey, JsonValue.Create(value));
        }
        return root;
    }

    /// <summary>Range une valeur sous une clé PHP (« a[b][] »), en créant les niveaux intermédiaires.</summary>
    public static void Set(JsonObject root, string rawKey, JsonNode? value)
    {
        int bracket = rawKey.IndexOf('[');
        string name = bracket < 0 ? rawKey : rawKey[..bracket];
        if (name.Length == 0)
        {
            return;
        }

        List<string> path = [name];
        if (bracket >= 0)
        {
            string rest = rawKey[bracket..];
            while (rest.StartsWith('['))
            {
                int close = rest.IndexOf(']');
                if (close < 0)
                {
                    break;
                }
                path.Add(rest[1..close]);
                rest = rest[(close + 1)..];
            }
        }

        JsonObject current = root;
        for (int i = 0; i < path.Count; i++)
        {
            string key = path[i];
            if (key.Length == 0)
            {
                // [] : indice suivant, comme PHP.
                key = (current.Select(kv => int.TryParse(kv.Key, out int k) ? k : -1).DefaultIfEmpty(-1).Max() + 1).ToString();
            }
            if (i == path.Count - 1)
            {
                current[key] = value;
                return;
            }
            if (current[key] is not JsonObject next)
            {
                next = [];
                current[key] = next;
            }
            current = next;
        }
    }

    /// <summary>Formulaire application/x-www-form-urlencoded : même syntaxe que l'URL.</summary>
    public static JsonObject ParseForm(IFormCollection form)
    {
        JsonObject root = [];
        foreach ((string key, Microsoft.Extensions.Primitives.StringValues values) in form)
        {
            foreach (string? v in values)
            {
                Set(root, key, JsonValue.Create(v ?? string.Empty));
            }
        }
        return root;
    }
}
