using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GlpiNg.Web.Api.Glpi;

/// <summary>Colonne d'une table GLPI, telle que déclarée dans <c>install/mysql/glpi-empty.sql</c>.</summary>
public sealed record GlpiColumn(string Name, string SqlType, bool Nullable, string? Default, bool HasDefault, bool AutoIncrement)
{
    /// <summary>Entier (tinyint, int, bigint...) : GLPI le rend comme nombre, jamais comme texte.</summary>
    public bool IsInteger => SqlType.StartsWith("tinyint", StringComparison.Ordinal)
                             || SqlType.StartsWith("smallint", StringComparison.Ordinal)
                             || SqlType.StartsWith("mediumint", StringComparison.Ordinal)
                             || SqlType.StartsWith("int", StringComparison.Ordinal)
                             || SqlType.StartsWith("bigint", StringComparison.Ordinal);

    public bool IsDecimal => SqlType.StartsWith("decimal", StringComparison.Ordinal);

    public bool IsFloat => SqlType.StartsWith("float", StringComparison.Ordinal) || SqlType.StartsWith("double", StringComparison.Ordinal);

    public bool IsDateTime => SqlType.StartsWith("timestamp", StringComparison.Ordinal) || SqlType.StartsWith("datetime", StringComparison.Ordinal);

    public bool IsDate => SqlType == "date";

    public bool IsTime => SqlType == "time";

    /// <summary>Nombre de décimales d'un <c>decimal(20,4)</c> — GLPI le rend comme texte, « 0.0000 ».</summary>
    public int Scale
    {
        get
        {
            int comma = SqlType.IndexOf(',');
            return comma > 0 && int.TryParse(SqlType.AsSpan(comma + 1, SqlType.Length - comma - 2), out int scale) ? scale : 0;
        }
    }
}

/// <summary>Option de recherche brute d'un itemtype (Search::getOptions), réduite à ce qui sert à l'évaluer.</summary>
public sealed class GlpiSearchOption
{
    public int Id { get; init; }
    public required string Table { get; init; }
    public required string Field { get; init; }
    public string? LinkField { get; init; }
    public string? Datatype { get; init; }
    public JsonObject? JoinParams { get; init; }
    public bool NoSearch { get; init; }
    public bool NoDisplay { get; init; }
    public bool HasComputation { get; init; }

    /// <summary>« equals » compare le champ lui-même plutôt que l'identifiant de la valeur.</summary>
    public bool SearchEqualsOnField { get; init; }
}

/// <summary>Ce que GLPI sait d'un itemtype : sa table, ses liens HATEOAS et ses options de recherche.</summary>
public sealed class GlpiItemtypeInfo
{
    public required string Name { get; init; }
    public string? Table { get; init; }
    public IReadOnlyList<string> Hateoas { get; init; } = [];
    public bool EntityAssign { get; init; }
    public bool MaybeRecursive { get; init; }
    public bool MaybeDeleted { get; init; }
    public bool MaybeTemplate { get; init; }
    public bool IsItil { get; init; }
    public bool IsTree { get; init; }
    public string NameField { get; init; } = "name";
    public string? ForeignKey { get; init; }
    public IReadOnlyList<int> DefaultToView { get; init; } = [];
    public int EntityOption { get; init; }
    public int IdOption { get; init; }

    /// <summary>Réponse de listSearchOptions, clés et ordre compris.</summary>
    public required JsonObject SearchOptions { get; init; }

    public required IReadOnlyDictionary<int, GlpiSearchOption> RawSearchOptions { get; init; }
}

/// <summary>
/// Métadonnées de GLPI embarquées dans l'assembly (dossier Metadata), produites depuis une version
/// publiée de GLPI par <c>tools/glpi-api-metadata/generate.php</c> : ce n'est pas une réécriture à
/// la main, c'est la sortie du code de GLPI lui-même. Les deux API s'y adossent — la v1 pour les
/// colonnes, options de recherche et liens, la v2 pour ses schémas et sa documentation OpenAPI.
///
/// Chargées une fois, à la première utilisation de l'API.
/// </summary>
public sealed class GlpiMetadata
{
    private static readonly Lazy<GlpiMetadata> LazyInstance = new(() => new GlpiMetadata());

    public static GlpiMetadata Instance => LazyInstance.Value;

    private GlpiMetadata()
    {
        Tables = LoadTables();
        (Itemtypes, TableToItemtype) = LoadItemtypes();

        JsonObject hlapi = (JsonObject)Load("hlapi.json")!;
        HlVersions = [.. hlapi["versions"]!.AsArray()
            .Select(v => (string)v!["version"]!)
            .Where(v => v.StartsWith("2.", StringComparison.Ordinal))];
        HlSchemas = hlapi["schemas"]!.AsObject()
            .ToDictionary(p => p.Key, p => p.Value!.AsObject(), StringComparer.Ordinal);

        Defaults = (JsonObject)Load("defaults.json")!;
        GlpiVersion = (string?)Defaults["glpi_version"] ?? "11.0.0";
    }

    /// <summary>Version de GLPI dont les métadonnées sont issues (et que les API annoncent).</summary>
    public string GlpiVersion { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<GlpiColumn>> Tables { get; }

    /// <summary>Itemtypes, par nom insensible à la casse comme <c>getItemtype</c> de GLPI.</summary>
    public IReadOnlyDictionary<string, GlpiItemtypeInfo> Itemtypes { get; }

    public IReadOnlyDictionary<string, string> TableToItemtype { get; }

    /// <summary>Versions de l'API v2 (« 2.0.0 » ... « 2.3.0 »), de la plus ancienne à la plus récente.</summary>
    public IReadOnlyList<string> HlVersions { get; }

    /// <summary>Schémas de l'API v2 par version, avec leurs métadonnées <c>x-field</c> / <c>x-join</c>.</summary>
    public IReadOnlyDictionary<string, JsonObject> HlSchemas { get; }

    public JsonObject Defaults { get; }

    public GlpiItemtypeInfo? FindItemtype(string? name)
        => name is not null && Itemtypes.TryGetValue(name, out GlpiItemtypeInfo? info) ? info : null;

    public string? ItemtypeForTable(string table) => TableToItemtype.GetValueOrDefault(table);

    public IReadOnlyList<GlpiColumn> ColumnsOf(string table) => Tables.GetValueOrDefault(table) ?? [];

    /// <summary>Documentation de l'API v1 (apirest.md de GLPI), servie à la racine de l'API.</summary>
    public string ApiRestDocumentation => LoadText("apirest.md");

    /// <summary>Documentation OpenAPI d'une version de l'API v2, telle que GLPI la génère (non parsée).</summary>
    public string OpenApiDocument(string version) => LoadText($"openapi-{version}.json");

    private readonly Dictionary<string, JsonObject> _openApiCache = [];

    /// <summary>Documentation OpenAPI parsée, pour le routage de l'API v2.</summary>
    public JsonObject OpenApi(string version)
    {
        lock (_openApiCache)
        {
            if (!_openApiCache.TryGetValue(version, out JsonObject? doc))
            {
                doc = JsonNode.Parse(OpenApiDocument(version))!.AsObject();
                _openApiCache[version] = doc;
            }
            return doc;
        }
    }

    private static Dictionary<string, IReadOnlyList<GlpiColumn>> LoadTables()
    {
        JsonObject root = (JsonObject)Load("tables.json")!;
        Dictionary<string, IReadOnlyList<GlpiColumn>> tables = new(StringComparer.Ordinal);
        foreach ((string table, JsonNode? cols) in root)
        {
            tables[table] = [.. cols!.AsArray().Select(c => new GlpiColumn(
                (string)c![0]!, (string)c[1]!, (bool)c[2]!, c[3]?.ToString(), (bool)c[4]!, (bool)c[5]!))];
        }
        return tables;
    }

    private static (Dictionary<string, GlpiItemtypeInfo>, Dictionary<string, string>) LoadItemtypes()
    {
        JsonObject root = (JsonObject)Load("itemtypes.json")!;
        Dictionary<string, GlpiItemtypeInfo> itemtypes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> byTable = new(StringComparer.Ordinal);

        foreach ((string name, JsonNode? node) in root)
        {
            JsonObject entry = node!.AsObject();
            Dictionary<int, GlpiSearchOption> raw = [];
            if (entry["raw"] is JsonObject rawOptions)
            {
                foreach ((string key, JsonNode? opt) in rawOptions)
                {
                    if (!int.TryParse(key, out int id) || opt is not JsonObject o || o["table"] is null || o["field"] is null)
                    {
                        continue;
                    }
                    raw[id] = new GlpiSearchOption
                    {
                        Id = id,
                        Table = (string)o["table"]!,
                        Field = (string)o["field"]!,
                        LinkField = o["linkfield"]?.GetValueKind() == JsonValueKind.String ? (string?)o["linkfield"] : null,
                        Datatype = o["datatype"]?.GetValueKind() == JsonValueKind.String ? (string?)o["datatype"] : null,
                        JoinParams = o["joinparams"] as JsonObject,
                        NoSearch = o["nosearch"]?.GetValueKind() == JsonValueKind.True,
                        NoDisplay = o["nodisplay"]?.GetValueKind() == JsonValueKind.True,
                        HasComputation = o["computation"] is not null,
                        SearchEqualsOnField = o["searchequalsonfield"]?.GetValueKind() == JsonValueKind.True,
                    };
                }
            }

            string? table = entry["table"]?.GetValueKind() == JsonValueKind.String ? (string?)entry["table"] : null;
            GlpiItemtypeInfo info = new()
            {
                Name = name,
                Table = table,
                Hateoas = entry["hateoas"] is JsonArray h ? [.. h.Select(x => (string)x!)] : [],
                EntityAssign = entry["entity_assign"]?.GetValueKind() == JsonValueKind.True,
                MaybeRecursive = entry["maybe_recursive"]?.GetValueKind() == JsonValueKind.True,
                MaybeDeleted = entry["maybe_deleted"]?.GetValueKind() == JsonValueKind.True,
                MaybeTemplate = entry["maybe_template"]?.GetValueKind() == JsonValueKind.True,
                IsItil = entry["is_itil"]?.GetValueKind() == JsonValueKind.True,
                IsTree = entry["is_tree"]?.GetValueKind() == JsonValueKind.True,
                NameField = (string?)entry["name_field"] ?? "name",
                ForeignKey = (string?)entry["fk"],
                DefaultToView = entry["toview"] is JsonArray tv ? [.. tv.Select(x => (int)x!)] : [1],
                EntityOption = (int?)entry["entity_opt"] ?? 0,
                IdOption = (int?)entry["id_opt"] ?? 0,
                SearchOptions = entry["searchoptions"] as JsonObject ?? [],
                RawSearchOptions = raw,
            };

            itemtypes[name] = info;
            if (table is not null)
            {
                byTable.TryAdd(table, name);
            }
        }

        return (itemtypes, byTable);
    }

    private static JsonNode? Load(string name) => JsonNode.Parse(LoadText(name));

    private static string LoadText(string name)
    {
        string resource = $"GlpiNg.Web.Api.Glpi.Metadata.{name}.gz";
        using Stream stream = typeof(GlpiMetadata).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Ressource embarquée introuvable : {resource}");
        using GZipStream gzip = new(stream, CompressionMode.Decompress);
        using StreamReader reader = new(gzip);
        return reader.ReadToEnd();
    }
}
