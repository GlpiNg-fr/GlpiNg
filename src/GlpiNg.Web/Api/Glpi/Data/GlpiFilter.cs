namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>Opérateur de comparaison sur une colonne GLPI.</summary>
public enum GlpiOp
{
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,

    /// <summary>Motif SQL LIKE (<c>%</c>, <c>_</c>), insensible à la casse comme les collations de GLPI.</summary>
    Like,
    NotLike,
    IsNull,
    NotNull,

    /// <summary>Valeur = collection de valeurs.</summary>
    In,
    NotIn,
}

/// <summary>
/// Condition sur les lignes d'une table GLPI, exprimée en colonnes et valeurs GLPI
/// (<c>entities_id = 0</c> désigne l'entité racine, <c>manufacturers_id</c> un identifiant d'intitulé...).
/// C'est la table virtuelle qui la traduit vers le modèle GlpiNg — en SQL quand elle le peut,
/// en mémoire sinon. Recherche v1, <c>searchText</c> et filtres RSQL de la v2 s'y ramènent tous.
/// </summary>
public abstract record GlpiFilter
{
    public static GlpiFilter True { get; } = new GlpiConst(true);

    public static GlpiFilter False { get; } = new GlpiConst(false);

    public static GlpiFilter Col(string column, GlpiOp op, object? value = null) => new GlpiColumnFilter(column, op, value);

    public static GlpiFilter IdIn(IEnumerable<int> ids) => new GlpiColumnFilter("id", GlpiOp.In, ids.Cast<object?>().ToList());

    public static GlpiFilter And(params GlpiFilter?[] items) => Combine(items, and: true);

    public static GlpiFilter Or(params GlpiFilter?[] items) => Combine(items, and: false);

    public static GlpiFilter Not(GlpiFilter inner) => inner switch
    {
        GlpiConst c => new GlpiConst(!c.Value),
        GlpiNot n => n.Inner,
        _ => new GlpiNot(inner),
    };

    private static GlpiFilter Combine(IEnumerable<GlpiFilter?> items, bool and)
    {
        List<GlpiFilter> list = [];
        foreach (GlpiFilter? item in items)
        {
            switch (item)
            {
                case null:
                    continue;
                case GlpiConst c when c.Value == and:
                    // Élément neutre (vrai pour ET, faux pour OU).
                    continue;
                case GlpiConst c:
                    return c;
                case GlpiAnd a when and:
                    list.AddRange(a.Items);
                    break;
                case GlpiOr o when !and:
                    list.AddRange(o.Items);
                    break;
                default:
                    list.Add(item);
                    break;
            }
        }

        return list.Count switch
        {
            0 => new GlpiConst(and),
            1 => list[0],
            _ => and ? new GlpiAnd(list) : new GlpiOr(list),
        };
    }

    /// <summary>Colonnes citées par la condition.</summary>
    public IEnumerable<string> Columns() => this switch
    {
        GlpiColumnFilter c => [c.Column],
        GlpiAnd a => a.Items.SelectMany(i => i.Columns()),
        GlpiOr o => o.Items.SelectMany(i => i.Columns()),
        GlpiNot n => n.Inner.Columns(),
        _ => [],
    };
}

public sealed record GlpiColumnFilter(string Column, GlpiOp Op, object? Value) : GlpiFilter;

public sealed record GlpiAnd(IReadOnlyList<GlpiFilter> Items) : GlpiFilter;

public sealed record GlpiOr(IReadOnlyList<GlpiFilter> Items) : GlpiFilter;

public sealed record GlpiNot(GlpiFilter Inner) : GlpiFilter;

public sealed record GlpiConst(bool Value) : GlpiFilter;

/// <summary>Tri sur une colonne GLPI.</summary>
public sealed record GlpiSort(string Column, bool Descending);
