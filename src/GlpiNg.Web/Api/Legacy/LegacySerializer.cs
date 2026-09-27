using System.Text.Json.Nodes;
using GlpiNg.Web.Api.Glpi;
using GlpiNg.Web.Api.Glpi.Data;

namespace GlpiNg.Web.Api.Legacy;

/// <summary>Options de rendu d'un élément (paramètres de getItem / getItems).</summary>
public sealed record LegacyRenderOptions(bool ExpandDropdowns, bool GetHateoas, IReadOnlyList<string> AddKeysNames);

/// <summary>
/// Rendu d'une ligne telle que l'API v1 de GLPI la renvoie : toutes les colonnes de la table
/// (<c>$item-&gt;fields</c>), champs sensibles retirés, puis API::parseDropdowns (noms à la place des
/// identifiants, liens HATEOAS) et liens vers les sous-éléments.
/// </summary>
public static class LegacySerializer
{
    /// <summary>Champs jamais divulgués (unsetUndisclosedFields des classes GLPI concernées).</summary>
    private static readonly Dictionary<string, string[]> Undisclosed = new(StringComparer.Ordinal)
    {
        ["glpi_users"] = ["password", "api_token", "cookie_token", "password_forget_token", "personal_token", "2fa", "password_history"],
        ["glpi_authldaps"] = ["rootdn_passwd"],
        ["glpi_mailcollectors"] = ["passwd"],
        ["glpi_snmpcredentials"] = ["auth_passphrase", "priv_passphrase"],
        ["glpi_apiclients"] = ["app_token"],
        ["glpi_oauthclients"] = ["secret"],
    };

    public static bool IsUndisclosed(string table, string field)
        => Undisclosed.TryGetValue(table, out string[]? fields) && fields.Contains(field);

    /// <summary>Colonnes de la table GLPI, valeurs du modèle ou valeurs par défaut de GLPI.</summary>
    public static JsonObject Fields(string table, GlpiRow row)
    {
        JsonObject fields = [];
        foreach (GlpiColumn column in GlpiMetadata.Instance.ColumnsOf(table))
        {
            if (IsUndisclosed(table, column.Name))
            {
                continue;
            }
            object? value = row.TryGetValue(column.Name, out object? v) ? v : GlpiDefaults.DefaultValue(table, column.Name);
            fields[column.Name] = GlpiNames.Format(column, value);
        }
        return fields;
    }

    /// <summary>Rendu complet d'une ligne : champs, liste déroulantes, liens, noms des clés.</summary>
    public static async Task<JsonObject> RenderAsync(GlpiDataContext ctx, string apiUrl, string itemtype, string table,
        GlpiRow row, LegacyRenderOptions options, CancellationToken ct)
    {
        JsonObject fields = Fields(table, row);
        JsonObject? keysNames = options.AddKeysNames.Count > 0 ? await FriendlyNamesAsync(ctx, itemtype, table, fields, options.AddKeysNames, ct) : null;
        await ParseDropdownsAsync(ctx, apiUrl, fields, options, ct);

        if (options.GetHateoas)
        {
            JsonArray links = fields["links"] as JsonArray ?? [];
            foreach (string hclass in GlpiMetadata.Instance.FindItemtype(itemtype)?.Hateoas ?? [])
            {
                links.Add(new JsonObject { ["rel"] = hclass, ["href"] = $"{apiUrl}/{itemtype}/{row.Id}/{hclass}/" });
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
        return fields;
    }

    /// <summary>API::parseDropdowns : liens HATEOAS et, si demandé, noms à la place des identifiants.</summary>
    public static async Task ParseDropdownsAsync(GlpiDataContext ctx, string apiUrl, JsonObject fields, LegacyRenderOptions options, CancellationToken ct)
    {
        JsonArray links = fields["links"] as JsonArray ?? [];
        foreach (string key in fields.Select(f => f.Key).ToList())
        {
            if (!GlpiNames.IsForeignKey(key))
            {
                continue;
            }

            // Transformations propres à certaines clés, comme dans GLPI.
            string fk = key;
            if (key == "items_id" && fields["itemtype"]?.ToString() is { } itemtype && GlpiNames.ForeignKeyForItemtype(itemtype) is { } itemFk)
            {
                fk = itemFk;
            }
            else if (key == "auths_id" && (int?)fields["authtype"] == 3)
            {
                fk = "authldaps_id";
            }
            else if (key == "default_requesttypes_id")
            {
                fk = "requesttypes_id";
            }
            else if (key == "mainitems_id" && fields["mainitemtype"]?.ToString() is { } mainType && GlpiNames.ForeignKeyForItemtype(mainType) is { } mainFk)
            {
                fk = mainFk;
            }

            if (fields[key] is not JsonValue v || !v.TryGetValue(out long id) || !(id > 0 || (fk == "entities_id" && id >= 0)))
            {
                continue;
            }

            string table = GlpiNames.TableForForeignKey(fk);
            string? target = GlpiMetadata.Instance.ItemtypeForTable(table);
            if (target is null)
            {
                continue;
            }
            if (options.GetHateoas)
            {
                links.Add(new JsonObject { ["rel"] = target, ["href"] = $"{apiUrl}/{target}/{id}" });
            }
            if (options.ExpandDropdowns)
            {
                fields[key] = await GlpiNames.DropdownNameAsync(ctx, table, (int)id, ct);
            }
        }
        if (links.Count > 0)
        {
            fields["links"] = links;
        }
    }

    /// <summary>API::getFriendlyNames : noms des clés étrangères demandées (et de l'élément pour « id »).</summary>
    private static async Task<JsonObject> FriendlyNamesAsync(GlpiDataContext ctx, string itemtype, string table,
        JsonObject fields, IReadOnlyList<string> keys, CancellationToken ct)
    {
        JsonObject names = [];
        foreach (string key in keys)
        {
            if (fields[key] is not JsonValue v || !v.TryGetValue(out long id))
            {
                continue;
            }
            string refTable = key == "id" ? table : GlpiNames.TableForForeignKey(key);
            names[key] = key == "id" || GlpiNames.IsForeignKey(key)
                ? await GlpiNames.DropdownNameAsync(ctx, refTable, (int)id, ct)
                : null;
        }
        return names;
    }
}
