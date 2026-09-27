using System.Linq.Expressions;
using System.Reflection;

namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>
/// Déclaration d'une table GLPI virtuelle, colonne par colonne :
/// <code>
/// Table&lt;Computer&gt;("glpi_computers", c =&gt; c.Id)
///     .Entity(c =&gt; c.EntityId, c =&gt; c.IsRecursive)
///     .Col("name", c =&gt; c.Name)
///     .Fk("locations_id", c =&gt; c.LocationId)
///     .Named("manufacturers_id", c =&gt; c.Manufacturer, "glpi_manufacturers")
/// </code>
/// Seules les colonnes déclarées sont alimentées ; les autres colonnes de la table GLPI existent
/// quand même dans les réponses, avec la valeur par défaut de GLPI (voir <see cref="GlpiDefaults"/>).
/// </summary>
public sealed class GlpiTableBuilder<T>(EfGlpiTable<T> table) where T : class
{
    public EfGlpiTable<T> Table { get; } = table;

    /// <summary>Colonne alimentée par une expression ; inscriptible si c'est une propriété simple.</summary>
    public GlpiTableBuilder<T> Col<TValue>(string name, Expression<Func<T, TValue>> expr, bool readOnly = false, bool wallClock = false)
    {
        bool isDate = (Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue)) == typeof(DateTime);
        GlpiColumnMap<T> column = new()
        {
            Name = name,
            Expr = expr,
            Read = (e, v, ctx) => v,
            IsUtcDate = isDate && !wallClock,
        };
        if (!readOnly)
        {
            column.Write = SetterFor(name, expr, isDate && !wallClock);
        }
        Table.AddColumn(column);
        return this;
    }

    /// <summary>Clé étrangère nullable : GLPI écrit 0 pour « aucune ».</summary>
    public GlpiTableBuilder<T> Fk(string name, Expression<Func<T, int?>> expr, bool readOnly = false)
    {
        PropertyInfo? prop = SimpleProperty(expr);
        Table.AddColumn(new GlpiColumnMap<T>
        {
            Name = name,
            Expr = expr,
            Read = (e, v, ctx) => v is int i ? (long)i : 0L,
            Translate = (ctx, body, op, value) => EfGlpiTable<T>.TranslateDefault(ctx, body, op, ZeroToNull(value), isUtcDate: false),
            Write = readOnly || prop is null ? null : (e, v, ctx) =>
            {
                prop.SetValue(e, GlpiValue.TryToLong(v, out long id) && id > 0 ? (int)id : null);
            },
        });
        return this;
    }

    /// <summary>Entité (<c>entities_id</c>, 0 = racine) et, si le modèle l'a, <c>is_recursive</c>.</summary>
    public GlpiTableBuilder<T> Entity(Expression<Func<T, int?>> entityId, Expression<Func<T, bool>>? isRecursive = null, bool readOnly = false)
    {
        PropertyInfo? prop = SimpleProperty(entityId);
        Table.AddColumn(new GlpiColumnMap<T>
        {
            Name = "entities_id",
            Expr = entityId,
            Read = (e, v, ctx) => (long)ctx.ToApiEntity(v as int?),
            Translate = (ctx, body, op, value) => TranslateEntity(ctx, body, op, value),
            Write = readOnly || prop is null ? null : (e, v, ctx) =>
            {
                if (GlpiValue.TryToLong(v, out long id) && id >= 0)
                {
                    prop.SetValue(e, ctx.FromApiEntity(id));
                }
            },
        });
        if (isRecursive is not null)
        {
            Col("is_recursive", isRecursive, readOnly);
        }
        return this;
    }

    /// <summary>
    /// Clé étrangère que GlpiNg stocke comme texte (le nom de la valeur, comme <c>Computer.Manufacturer</c>) :
    /// GLPI en voit l'identifiant de la valeur de <paramref name="refTable"/> portant ce nom, 0 si aucune.
    /// </summary>
    public GlpiTableBuilder<T> Named(string name, Expression<Func<T, string?>> expr, string refTable, string refColumn = "name", bool readOnly = false)
    {
        PropertyInfo? prop = SimpleProperty(expr);
        Table.AddColumn(new GlpiColumnMap<T>
        {
            Name = name,
            Expr = expr,
            Lookups = [(refTable, refColumn)],
            Read = (e, v, ctx) => (long)ctx.Lookup(refTable, refColumn).IdOf(v as string),
            Translate = (ctx, body, op, value) => TranslateNamed(ctx.Lookup(refTable, refColumn), ctx, body, op, value),
            Write = readOnly || prop is null ? null : (e, v, ctx) =>
            {
                // Identifiant (le cas GLPI), ou, par tolérance, directement le nom de la valeur.
                if (GlpiValue.TryToLong(v, out long id))
                {
                    prop.SetValue(e, id > 0 ? ctx.Lookup(refTable, refColumn).NameOf((int)id) : null);
                }
                else if (v is string text)
                {
                    prop.SetValue(e, string.IsNullOrWhiteSpace(text) ? null : text.Trim());
                }
            },
        });
        return this;
    }

    /// <summary>Colonne à valeur fixe (<c>itemtype</c> d'une table de liaison, par exemple).</summary>
    public GlpiTableBuilder<T> Const(string name, object? value)
    {
        Table.AddColumn(new GlpiColumnMap<T>
        {
            Name = name,
            Expr = null,
            Read = (e, v, ctx) => value,
            Translate = (ctx, body, op, expected) => Expression.Constant(GlpiFilterEvaluator.Matches(value, op, expected)),
        });
        return this;
    }

    /// <summary>Colonne calculée en mémoire à partir de l'entité (conditions et tris évalués en mémoire).</summary>
    public GlpiTableBuilder<T> Computed(string name, Func<T, GlpiDataContext, object?> read, Action<T, object?, GlpiDataContext>? write = null,
        params (string Table, string Column)[] lookups)
    {
        Table.AddColumn(new GlpiColumnMap<T>
        {
            Name = name,
            Expr = null,
            Read = (e, v, ctx) => read(e, ctx),
            Write = write,
            Lookups = lookups,
        });
        return this;
    }

    /// <summary>Colonne à traduction SQL sur mesure.</summary>
    public GlpiTableBuilder<T> Custom(GlpiColumnMap<T> column)
    {
        Table.AddColumn(column);
        return this;
    }

    /// <summary>Corbeille : <c>is_deleted</c>, et suppression sans purge qui y met l'objet.</summary>
    public GlpiTableBuilder<T> Trash(Expression<Func<T, bool>> isDeleted)
    {
        Col("is_deleted", isDeleted);
        Table.TrashFlag = SimpleProperty(isDeleted);
        return this;
    }

    /// <summary>Restreint la table aux lignes répondant à la condition (discriminant, visibilité du parent...).</summary>
    public GlpiTableBuilder<T> Where(Expression<Func<T, bool>> filter)
    {
        Table.AddBaseFilter(_ => filter);
        return this;
    }

    public GlpiTableBuilder<T> Where(Func<GlpiDataContext, Expression<Func<T, bool>>> filter)
    {
        Table.AddBaseFilter(filter);
        return this;
    }

    /// <summary>Initialisation d'une entité créée par l'API (discriminant, dates, auteur...).</summary>
    public GlpiTableBuilder<T> OnCreate(Action<T, GlpiDataContext> action)
    {
        Table.AddOnCreate(action);
        return this;
    }

    /// <summary>Appliqué à chaque création et modification (date de modification...).</summary>
    public GlpiTableBuilder<T> OnSave(Action<T, GlpiDataContext> action)
    {
        Table.AddOnSave(action);
        return this;
    }

    /// <summary>Traitement asynchrone avant enregistrement (mot de passe, règles propres au modèle...).</summary>
    public GlpiTableBuilder<T> BeforeSave(Func<T, GlpiDataContext, GlpiInput, CancellationToken, Task> action)
    {
        Table.AddBeforeSave(action);
        return this;
    }

    public GlpiTableBuilder<T> ReadOnly()
    {
        Table.IsWritable = false;
        return this;
    }

    public GlpiTableBuilder<T> AfterSave(Action<GlpiDataContext> action)
    {
        Table.AddAfterSave(action);
        return this;
    }

    public GlpiTableBuilder<T> Preload(Func<GlpiDataContext, CancellationToken, Task> preload)
    {
        Table.AddPreload(preload);
        return this;
    }

    /// <summary>
    /// Arborescence (CommonTreeDropdown de GLPI) : <c>completename</c> (« Parent &gt; Enfant ») et
    /// <c>level</c>, calculés à partir de la colonne parente déjà déclarée.
    /// </summary>
    public GlpiTableBuilder<T> Tree(string parentColumn, string nameColumn = "name")
    {
        string key = "tree:" + Table.Name;
        Preload(async (ctx, ct) =>
        {
            if (ctx.Cache.ContainsKey(key))
            {
                return;
            }
            Dictionary<int, object?> names = await Table.GetColumnAsync(ctx, nameColumn, null, ct);
            Dictionary<int, object?> parents = await Table.GetColumnAsync(ctx, parentColumn, null, ct);
            ctx.Cache[key] = names.ToDictionary(
                n => n.Key,
                n => (Name: GlpiValue.ToText(n.Value) ?? string.Empty,
                      Parent: GlpiValue.TryToLong(parents.GetValueOrDefault(n.Key), out long p) ? (int)p : 0));
        });

        List<string> Path(int id, GlpiDataContext ctx)
        {
            Dictionary<int, (string Name, int Parent)> tree = (Dictionary<int, (string Name, int Parent)>)ctx.Cache[key];
            List<string> path = [];
            HashSet<int> seen = [];
            int? current = id;
            while (current is int c && tree.TryGetValue(c, out (string Name, int Parent) node) && seen.Add(c))
            {
                path.Insert(0, node.Name);
                current = node.Parent == c ? null : node.Parent;
            }
            return path;
        }

        Computed("completename", (e, ctx) => string.Join(" > ", Path(Table.ApiIdOfEntity(ctx, e), ctx)));
        Computed("level", (e, ctx) => (long)Math.Max(1, Path(Table.ApiIdOfEntity(ctx, e), ctx).Count));
        return this;
    }


    // --- Traductions ------------------------------------------------------------------------

    private static object? ZeroToNull(object? value) => value switch
    {
        System.Collections.IEnumerable list and not string => list.Cast<object?>().Select(ZeroToNull).ToList(),
        _ => GlpiValue.TryToLong(value, out long l) && l == 0 ? null : value,
    };

    private static Expression? TranslateEntity(GlpiDataContext ctx, Expression body, GlpiOp op, object? value)
    {
        // 0 (racine) couvre aussi les objets sans entité, que GlpiNg traite comme visibles partout.
        object? Map(object? v) => GlpiValue.TryToLong(v, out long l) ? ctx.FromApiEntity(l) : v;

        if (op is GlpiOp.Eq or GlpiOp.Ne && GlpiValue.TryToLong(value, out long id) && id == 0)
        {
            Expression isRoot = Expression.OrElse(
                Expression.Equal(body, Expression.Constant(null, body.Type)),
                Expression.Equal(body, Expression.Convert(Expression.Constant(ctx.RootEntityId), body.Type)));
            return op == GlpiOp.Eq ? isRoot : Expression.Not(isRoot);
        }

        object? mapped = value is System.Collections.IEnumerable list and not string ? list.Cast<object?>().Select(Map).ToList() : Map(value);
        Expression? translated = EfGlpiTable<T>.TranslateDefault(ctx, body, op, mapped, isUtcDate: false);
        if (translated is not null && op == GlpiOp.In && value is System.Collections.IEnumerable l0 and not string
            && l0.Cast<object?>().Any(v => GlpiValue.TryToLong(v, out long z) && z == 0))
        {
            translated = Expression.OrElse(translated, Expression.Equal(body, Expression.Constant(null, body.Type)));
        }
        return translated;
    }

    private static Expression? TranslateNamed(GlpiLookup lookup, GlpiDataContext ctx, Expression body, GlpiOp op, object? value)
    {
        Expression IsEmpty() => Expression.OrElse(
            Expression.Equal(body, Expression.Constant(null, typeof(string))),
            Expression.Equal(body, Expression.Constant(string.Empty)));

        switch (op)
        {
            case GlpiOp.Eq:
            case GlpiOp.Ne:
            {
                if (!GlpiValue.TryToLong(value, out long id))
                {
                    return Expression.Constant(op == GlpiOp.Ne);
                }
                Expression match;
                if (id == 0)
                {
                    match = IsEmpty();
                }
                else if (lookup.NameOf((int)id) is string name)
                {
                    match = EfGlpiTable<T>.TranslateDefault(ctx, body, GlpiOp.Eq, name, isUtcDate: false)!;
                }
                else
                {
                    match = Expression.Constant(false);
                }
                return op == GlpiOp.Eq ? match : Expression.Not(match);
            }
            case GlpiOp.In:
            case GlpiOp.NotIn:
            {
                if (value is not System.Collections.IEnumerable items || value is string)
                {
                    return null;
                }
                List<object?> names = [];
                bool withEmpty = false;
                foreach (object? item in items)
                {
                    if (!GlpiValue.TryToLong(item, out long id))
                    {
                        continue;
                    }
                    if (id == 0)
                    {
                        withEmpty = true;
                    }
                    else if (lookup.NameOf((int)id) is string name)
                    {
                        names.Add(name);
                    }
                }
                Expression match = names.Count > 0
                    ? EfGlpiTable<T>.TranslateDefault(ctx, body, GlpiOp.In, names, isUtcDate: false)!
                    : Expression.Constant(false);
                if (withEmpty)
                {
                    match = Expression.OrElse(match, IsEmpty());
                }
                return op == GlpiOp.In ? match : Expression.Not(match);
            }
            case GlpiOp.IsNull:
                return Expression.Constant(false);
            case GlpiOp.NotNull:
                return Expression.Constant(true);
            default:
                // Comparaison d'ordre ou LIKE sur un identifiant calculé : en mémoire.
                return null;
        }
    }

    // --- Accès aux propriétés ------------------------------------------------------------------

    private static PropertyInfo? SimpleProperty(LambdaExpression expr)
    {
        Expression body = expr.Body is UnaryExpression { NodeType: ExpressionType.Convert } u ? u.Operand : expr.Body;
        return body is MemberExpression { Member: PropertyInfo prop } member
               && member.Expression == expr.Parameters[0] && prop.CanWrite
            ? prop
            : null;
    }

    private static Action<T, object?, GlpiDataContext>? SetterFor(string name, LambdaExpression expr, bool utcDate)
    {
        PropertyInfo? prop = SimpleProperty(expr);
        if (prop is null)
        {
            return null;
        }

        return (e, v, ctx) =>
        {
            Type type = prop.PropertyType;
            if (v is null && type.IsValueType && Nullable.GetUnderlyingType(type) is null)
            {
                // null vers un champ non nullable : GLPI stockerait 0 / chaîne vide.
                v = type == typeof(bool) ? false : 0L;
            }
            if (v is null && type == typeof(string) && !IsNullable(prop))
            {
                v = string.Empty;
            }
            if (!GlpiValue.TryConvert(v, type, out object? converted))
            {
                throw new GlpiWriteException($"Invalid value for field {name}");
            }
            if (utcDate && converted is DateTime dt)
            {
                converted = ctx.FromUserTime(dt);
            }
            prop.SetValue(e, converted);
        };
    }

    private static readonly NullabilityInfoContext Nullability = new();

    private static bool IsNullable(PropertyInfo prop)
    {
        lock (Nullability)
        {
            return Nullability.Create(prop).WriteState != NullabilityState.NotNull;
        }
    }
}
