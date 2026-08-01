using System.Collections;
using System.Reflection;

namespace GlpiNg.Web.Services;

/// <summary>Compare deux instances d'un même modèle de réglages propriété par propriété, pour alimenter <see cref="ConfigHistoryService"/>.</summary>
public static class SettingsDiff
{
    public static List<(string Field, string Old, string New)> Compare<T>(T before, T after)
    {
        var changes = new List<(string, string, string)>();

        foreach (PropertyInfo prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            string oldText = Describe(prop.GetValue(before));
            string newText = Describe(prop.GetValue(after));
            if (oldText != newText)
            {
                changes.Add((prop.Name, oldText, newText));
            }
        }

        return changes;
    }

    private static string Describe(object? value)
    {
        switch (value)
        {
            case null:
                return "-----";
            case bool b:
                return b ? "Oui" : "Non";
            case string s:
                return string.IsNullOrEmpty(s) ? "-----" : s;
            case IDictionary dict:
            {
                var pairs = dict.Keys.Cast<object>().Select(k => $"{k}={dict[k]}");
                return string.Join(", ", pairs);
            }
            case IEnumerable enumerable:
                return string.Join(", ", enumerable.Cast<object>());
            default:
                return value.ToString() ?? "-----";
        }
    }
}
