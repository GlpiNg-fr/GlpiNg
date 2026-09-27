using System.Linq.Expressions;

namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>
/// Catalogue des tables GLPI virtuelles : pour chaque table de GLPI que GlpiNg sait alimenter, la
/// correspondance colonne par colonne avec son modèle. Les déclarations sont réparties par domaine
/// (fichiers <c>GlpiTableCatalog.*.cs</c>).
///
/// Toute autre table de GLPI reste accessible, vide (<see cref="EmptyGlpiTable"/>) : l'itemtype est
/// valide, ses colonnes et ses options de recherche sont celles de GLPI, il n'a simplement aucune
/// ligne — comme dans une instance GLPI où ce type n'a jamais servi.
///
/// Singleton : les expressions sont construites une fois ; tout ce qui dépend de la requête passe
/// par le <see cref="GlpiDataContext"/>.
/// </summary>
public sealed partial class GlpiTableCatalog
{
    private readonly Dictionary<string, IGlpiTable> _tables = new(StringComparer.Ordinal);

    public GlpiTableCatalog()
    {
        RegisterAdministration();
        RegisterDropdowns();
        RegisterAssets();
        RegisterManagement();
        RegisterAssistance();
        RegisterTools();
    }

    public IGlpiTable Get(string table)
        => _tables.TryGetValue(table, out IGlpiTable? t) ? t : new EmptyGlpiTable(table);

    public bool IsBacked(string table) => _tables.ContainsKey(table);

    public IEnumerable<IGlpiTable> Backed => _tables.Values;

    private GlpiTableBuilder<T> Table<T>(string name, Expression<Func<T, int>> id) where T : class
    {
        EfGlpiTable<T> table = new(name, id);
        _tables[name] = table;
        return new GlpiTableBuilder<T>(table);
    }

    /// <summary>Source d'une <see cref="UnionGlpiTable"/> : table déclarée comme les autres, mais pas inscrite au catalogue.</summary>
    private static GlpiTableBuilder<T> Part<T>(string name, Expression<Func<T, int>> id) where T : class
        => new(new EfGlpiTable<T>(name, id));

    private void Union(string name, params IGlpiTable[] parts) => _tables[name] = new UnionGlpiTable(name, parts);

    /// <summary>Date/heure courante pour les colonnes date_mod/date_creation alimentées par l'API.</summary>
    private static DateTime Now => DateTime.UtcNow;
}
