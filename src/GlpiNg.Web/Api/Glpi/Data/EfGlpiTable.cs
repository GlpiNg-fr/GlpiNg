using System.Linq.Expressions;
using System.Reflection;
using GlpiNg.Modules.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>
/// Colonne GLPI d'une table virtuelle, alimentée par le modèle <typeparamref name="T"/>.
/// </summary>
public sealed class GlpiColumnMap<T> where T : class
{
    public required string Name { get; init; }

    /// <summary>Expression traduisible en SQL donnant la valeur (brute, avant conversion GLPI) ; null si calculée en mémoire.</summary>
    public LambdaExpression? Expr { get; init; }

    /// <summary>Valeur GLPI de la colonne pour une entité et, le cas échéant, sa valeur projetée par <see cref="Expr"/>.</summary>
    public required Func<T, object?, GlpiDataContext, object?> Read { get; init; }

    /// <summary>Traduction SQL d'une comparaison ; null = traduction par défaut depuis <see cref="Expr"/>.</summary>
    public Func<GlpiDataContext, Expression, GlpiOp, object?, Expression?>? Translate { get; init; }

    /// <summary>Écriture d'une valeur GLPI reçue ; null = colonne en lecture seule.</summary>
    public Action<T, object?, GlpiDataContext>? Write { get; set; }

    /// <summary>Tables de correspondance nom ↔ id à précharger avant lecture ou traduction.</summary>
    public IReadOnlyList<(string Table, string Column)> Lookups { get; init; } = [];

    /// <summary>Date stockée en UTC, échangée dans le fuseau de l'utilisateur.</summary>
    public bool IsUtcDate { get; init; }
}

/// <summary>Conteneur de valeur : EF paramètre une valeur lue sur un objet plutôt que de l'inscrire en dur dans le SQL.</summary>
internal sealed class GlpiParam<TValue>(TValue value)
{
    public TValue Value { get; } = value;
}

internal sealed class GlpiProjection<T>
{
    public T Entity { get; set; } = default!;
    public object?[] Values { get; set; } = [];
}

/// <summary>
/// Table GLPI virtuelle alimentée par une entité EF <typeparamref name="T"/> (voir <see cref="IGlpiTable"/>).
/// Les conditions, tris et paginations sont traduits en SQL tant que les colonnes citées ont une
/// expression traduisible ; sinon toute la requête est évaluée en mémoire, sur les lignes visibles
/// — correct, mais réservé aux petites tables (intitulés, catégories...).
/// </summary>
public sealed class EfGlpiTable<T> : IGlpiTable where T : class
{
    private readonly Dictionary<string, GlpiColumnMap<T>> _columns = new(StringComparer.Ordinal);
    private readonly List<Func<GlpiDataContext, Expression<Func<T, bool>>>> _baseFilters = [];
    private readonly List<Action<T, GlpiDataContext>> _onCreate = [];
    private readonly List<Action<T, GlpiDataContext>> _onSave = [];
    private readonly List<Func<T, GlpiDataContext, GlpiInput, CancellationToken, Task>> _beforeSave = [];
    private readonly List<Func<GlpiDataContext, CancellationToken, Task>> _preloads = [];
    private readonly List<Action<GlpiDataContext>> _afterSave = [];

    /// <summary>Appliqué une fois l'écriture enregistrée (invalidation de caches...).</summary>
    public void AddAfterSave(Action<GlpiDataContext> action) => _afterSave.Add(action);
    private readonly Expression<Func<T, int>> _idExpr;
    private readonly Func<T, int> _idGetter;
    private List<GlpiColumnMap<T>>? _projected;

    public EfGlpiTable(string name, Expression<Func<T, int>> id)
    {
        Name = name;
        _idExpr = id;
        _idGetter = id.Compile();
        AddColumn(new GlpiColumnMap<T>
        {
            Name = "id",
            Expr = id,
            Read = (e, v, ctx) => IdToApi(ctx, (int)v!),
            Translate = (ctx, body, op, value) => TranslateId(ctx, body, op, value),
        });
    }

    public string Name { get; }

    public Type? ClrType => typeof(T);

    public bool IsWritable { get; set; } = true;

    public bool HasTrash => TrashFlag is not null;

    /// <summary>Propriété « mise à la corbeille » (<c>is_deleted</c>), si le modèle en a une.</summary>
    public PropertyInfo? TrashFlag { get; set; }

    /// <summary>Identifiant GLPI ↔ identifiant GlpiNg (seules les entités diffèrent : racine = 0).</summary>
    public Func<GlpiDataContext, int, int>? IdToApiMap { get; set; }

    public Func<GlpiDataContext, int, int>? IdFromApiMap { get; set; }

    public IReadOnlyDictionary<string, GlpiColumnMap<T>> Columns => _columns;

    /// <summary>
    /// Visibilité décidée en mémoire, ligne par ligne (règle métier non traduisible en SQL, comme
    /// la visibilité d'un article). La table est alors toujours interrogée en mémoire.
    /// </summary>
    public Func<T, GlpiDataContext, bool>? RowFilter { get; set; }

    /// <summary>Mise en forme de la requête de base (Include des données dont <see cref="RowFilter"/> a besoin).</summary>
    public Func<IQueryable<T>, IQueryable<T>>? Shape { get; set; }

    public bool IsMapped(string column) => _columns.ContainsKey(column);

    public void AddColumn(GlpiColumnMap<T> column)
    {
        _columns[column.Name] = column;
        _projected = null;
    }

    public void AddBaseFilter(Func<GlpiDataContext, Expression<Func<T, bool>>> filter) => _baseFilters.Add(filter);

    public void AddOnCreate(Action<T, GlpiDataContext> action) => _onCreate.Add(action);

    public void AddOnSave(Action<T, GlpiDataContext> action) => _onSave.Add(action);

    public void AddBeforeSave(Func<T, GlpiDataContext, GlpiInput, CancellationToken, Task> action) => _beforeSave.Add(action);

    /// <summary>Chargement asynchrone préalable à la lecture des lignes (caches des colonnes calculées).</summary>
    public void AddPreload(Func<GlpiDataContext, CancellationToken, Task> preload) => _preloads.Add(preload);

    /// <summary>Identifiant GlpiNg (interne) d'une entité du modèle.</summary>
    public int IdOfEntity(T entity) => _idGetter(entity);

    /// <summary>Identifiant GLPI d'une entité du modèle (la racine des entités vaut 0).</summary>
    public int ApiIdOfEntity(GlpiDataContext ctx, T entity) => IdToApi(ctx, _idGetter(entity));

    private int IdToApi(GlpiDataContext ctx, int id) => IdToApiMap?.Invoke(ctx, id) ?? id;

    private int IdFromApi(GlpiDataContext ctx, int id) => IdFromApiMap?.Invoke(ctx, id) ?? id;

    // --- Lecture -----------------------------------------------------------------------------

    private IQueryable<T> BaseQuery(GlpiDataContext ctx, bool tracking = false)
    {
        IQueryable<T> query = tracking ? ctx.Db.Set<T>() : ctx.Db.Set<T>().AsNoTracking();
        foreach (Func<GlpiDataContext, Expression<Func<T, bool>>> filter in _baseFilters)
        {
            query = query.Where(filter(ctx));
        }
        return Shape is null ? query : Shape(query);
    }

    public async Task<GlpiPage> QueryAsync(GlpiDataContext ctx, GlpiFilter? filter, IReadOnlyList<GlpiSort>? sort, int start, int? limit, CancellationToken ct)
    {
        await PreloadLookupsAsync(ctx, (filter?.Columns() ?? []).Concat(sort?.Select(s => s.Column) ?? []), ct);

        Expression<Func<T, bool>>? predicate = filter is null ? null : TryTranslate(ctx, filter);
        if (predicate?.Body is ConstantExpression { Value: false })
        {
            // Condition toujours fausse (itemtype d'une autre table...) : inutile d'interroger la base.
            return new GlpiPage([], 0);
        }
        bool sortTranslatable = sort is null || sort.All(s => _columns.TryGetValue(s.Column, out GlpiColumnMap<T>? c) && c.Expr is not null);

        if ((filter is null || predicate is not null) && sortTranslatable && RowFilter is null)
        {
            IQueryable<T> query = BaseQuery(ctx);
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            int total = await query.CountAsync(ct);
            IQueryable<T> ordered = ApplySort(query, sort);
            IQueryable<int> ids = ordered.Select(_idExpr).Skip(start);
            if (limit is int l)
            {
                ids = ids.Take(l);
            }
            List<int> page = await ids.ToListAsync(ct);
            return new GlpiPage([.. page.Select(id => IdToApi(ctx, id))], total);
        }

        // Évaluation en mémoire : condition ou tri sur une colonne calculée.
        List<GlpiRow> rows = [.. (await LoadRowsAsync(ctx, BaseQuery(ctx), ct)).Values.Where(r => GlpiFilterEvaluator.Matches(r, filter))];
        IEnumerable<GlpiRow> sorted = rows;
        if (sort is { Count: > 0 })
        {
            IOrderedEnumerable<GlpiRow>? o = null;
            foreach (GlpiSort s in sort)
            {
                Func<GlpiRow, object?> key = r => r.GetValueOrDefault(s.Column);
                IComparer<object?> comparer = Comparer<object?>.Create(GlpiValue.Compare);
                o = o is null
                    ? (s.Descending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer))
                    : (s.Descending ? o.ThenByDescending(key, comparer) : o.ThenBy(key, comparer));
            }
            sorted = o!.ThenBy(r => r.Id);
        }
        else
        {
            sorted = rows.OrderBy(r => r.Id);
        }

        IEnumerable<GlpiRow> paged = sorted.Skip(start);
        if (limit is int max)
        {
            paged = paged.Take(max);
        }
        return new GlpiPage([.. paged.Select(r => r.Id)], rows.Count);
    }

    private IQueryable<T> ApplySort(IQueryable<T> query, IReadOnlyList<GlpiSort>? sort)
    {
        IOrderedQueryable<T>? ordered = null;
        foreach (GlpiSort s in sort ?? [])
        {
            LambdaExpression key = _columns[s.Column].Expr!;
            string method = ordered is null
                ? (s.Descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
                : (s.Descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));
            ordered = (IOrderedQueryable<T>)typeof(Queryable).GetMethods()
                .First(m => m.Name == method && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), key.ReturnType)
                .Invoke(null, [(object?)ordered ?? query, key])!;
        }

        // Ordre stable pour la pagination.
        return ordered is null ? query.OrderBy(_idExpr) : ordered.ThenBy(_idExpr);
    }

    public async Task<Dictionary<int, GlpiRow>> GetRowsAsync(GlpiDataContext ctx, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        List<int> internalIds = [.. ids.Select(id => IdFromApi(ctx, id)).Distinct()];
        Expression<Func<T, bool>> byId = BuildContains(_idExpr, internalIds);
        return await LoadRowsAsync(ctx, BaseQuery(ctx).Where(byId), ct);
    }

    public async Task<Dictionary<int, object?>> GetColumnAsync(GlpiDataContext ctx, string column, GlpiFilter? filter, CancellationToken ct)
    {
        if (!_columns.TryGetValue(column, out GlpiColumnMap<T>? map))
        {
            return [];
        }

        await PreloadLookupsAsync(ctx, [column, .. filter?.Columns() ?? []], ct);
        Expression<Func<T, bool>>? predicate = filter is null ? null : TryTranslate(ctx, filter);

        if (map.Expr is not null && map.Lookups.Count == 0 && (filter is null || predicate is not null) && column != "id" && RowFilter is null)
        {
            IQueryable<T> query = BaseQuery(ctx);
            if (predicate is not null)
            {
                query = query.Where(predicate);
            }

            // Projection (id, valeur) sans charger l'entité : c'est ce qui sert aux correspondances
            // nom ↔ id et aux jointures de la recherche, parfois sur de grosses tables.
            ParameterExpression p = _idExpr.Parameters[0];
            Expression value = Expression.Convert(Replace(map.Expr, p), typeof(object));
            NewExpression pair = Expression.New(
                typeof(KeyValuePair<int, object?>).GetConstructor([typeof(int), typeof(object)])!,
                _idExpr.Body, value);
            Expression<Func<T, KeyValuePair<int, object?>>> selector = Expression.Lambda<Func<T, KeyValuePair<int, object?>>>(pair, p);
            List<KeyValuePair<int, object?>> values = await query.Select(selector).ToListAsync(ct);

            Dictionary<int, object?> result = new(values.Count);
            foreach ((int id, object? raw) in values)
            {
                result[IdToApi(ctx, id)] = Normalize(map, map.Read(null!, raw, ctx), ctx);
            }
            return result;
        }

        Dictionary<int, GlpiRow> rows = await LoadRowsAsync(ctx, predicate is null ? BaseQuery(ctx) : BaseQuery(ctx).Where(predicate), ct);
        return rows.Values
            .Where(r => predicate is not null || GlpiFilterEvaluator.Matches(r, filter))
            .ToDictionary(r => r.Id, r => r.GetValueOrDefault(column));
    }

    private async Task<Dictionary<int, GlpiRow>> LoadRowsAsync(GlpiDataContext ctx, IQueryable<T> query, CancellationToken ct)
    {
        await PreloadLookupsAsync(ctx, _columns.Keys, ct);
        foreach (Func<GlpiDataContext, CancellationToken, Task> preload in _preloads)
        {
            await preload(ctx, ct);
        }

        List<GlpiColumnMap<T>> projected = _projected ??= [.. _columns.Values.Where(c => c.Expr is not null)];

        // Une seule requête : l'entité (pour les colonnes calculées) et les expressions des colonnes
        // (qui peuvent traverser des navigations, traduites en jointures).
        ParameterExpression p = _idExpr.Parameters[0];
        MemberInitExpression init = Expression.MemberInit(
            Expression.New(typeof(GlpiProjection<T>)),
            Expression.Bind(typeof(GlpiProjection<T>).GetProperty(nameof(GlpiProjection<T>.Entity))!, p),
            Expression.Bind(typeof(GlpiProjection<T>).GetProperty(nameof(GlpiProjection<T>.Values))!,
                Expression.NewArrayInit(typeof(object), projected.Select(c => Expression.Convert(Replace(c.Expr!, p), typeof(object))))));
        Expression<Func<T, GlpiProjection<T>>> selector = Expression.Lambda<Func<T, GlpiProjection<T>>>(init, p);

        List<GlpiProjection<T>> results = await query.Select(selector).ToListAsync(ct);

        Dictionary<int, GlpiRow> rows = new(results.Count);
        foreach (GlpiProjection<T> result in results)
        {
            if (RowFilter is not null && !RowFilter(result.Entity, ctx))
            {
                continue;
            }
            GlpiRow row = [];
            Dictionary<GlpiColumnMap<T>, object?> raws = [];
            for (int i = 0; i < projected.Count; i++)
            {
                raws[projected[i]] = result.Values[i];
            }
            foreach (GlpiColumnMap<T> column in _columns.Values)
            {
                object? raw = raws.GetValueOrDefault(column);
                row[column.Name] = Normalize(column, column.Read(result.Entity, raw, ctx), ctx);
            }
            rows[row.Id] = row;
        }
        return rows;
    }

    private static object? Normalize(GlpiColumnMap<T> column, object? value, GlpiDataContext ctx) => value switch
    {
        DateTime dt when column.IsUtcDate => ctx.ToUserTime(dt),
        DateTimeOffset dto => ctx.ToUserTime(dto.UtcDateTime),
        Enum e => Convert.ToInt64(e, System.Globalization.CultureInfo.InvariantCulture),
        int i => (long)i,
        short s => (long)s,
        byte b => (long)b,
        _ => value,
    };

    private async Task PreloadLookupsAsync(GlpiDataContext ctx, IEnumerable<string> columns, CancellationToken ct)
    {
        foreach (string name in columns.Distinct())
        {
            if (_columns.TryGetValue(name, out GlpiColumnMap<T>? column))
            {
                foreach ((string table, string col) in column.Lookups)
                {
                    await ctx.LookupAsync(table, col, ct);
                }
            }
        }
    }

    // --- Traduction des conditions -------------------------------------------------------------

    private Expression<Func<T, bool>>? TryTranslate(GlpiDataContext ctx, GlpiFilter filter)
    {
        ParameterExpression p = _idExpr.Parameters[0];
        Expression? body = Translate(ctx, filter, p);
        return body is null ? null : Expression.Lambda<Func<T, bool>>(body, p);
    }

    private Expression? Translate(GlpiDataContext ctx, GlpiFilter filter, ParameterExpression p)
    {
        switch (filter)
        {
            case GlpiConst c:
                return Expression.Constant(c.Value);
            case GlpiNot n:
                Expression? inner = Translate(ctx, n.Inner, p);
                return inner is null ? null : Expression.Not(inner);
            case GlpiAnd a:
                return Fold(a.Items, Expression.AndAlso);
            case GlpiOr o:
                return Fold(o.Items, Expression.OrElse);
            case GlpiColumnFilter c:
                if (!_columns.TryGetValue(c.Column, out GlpiColumnMap<T>? column))
                {
                    // Colonne GLPI non alimentée : sa valeur est celle par défaut de GLPI, comparée ici une fois pour toutes.
                    return Expression.Constant(GlpiFilterEvaluator.Matches(GlpiDefaults.DefaultValue(Name, c.Column), c.Op, c.Value));
                }
                if (column.Translate is not null)
                {
                    Expression target = column.Expr is null ? p : Replace(column.Expr, p);
                    return column.Translate(ctx, target, c.Op, c.Value);
                }
                return column.Expr is null ? null : TranslateDefault(ctx, Replace(column.Expr, p), c.Op, c.Value, column.IsUtcDate);
            default:
                return null;
        }

        Expression? Fold(IReadOnlyList<GlpiFilter> items, Func<Expression, Expression, BinaryExpression> combine)
        {
            // Élément absorbant du ET (faux) ou du OU (vrai), neutre dans l'autre cas : plié ici pour
            // qu'une condition constante reste reconnaissable (et la requête évitée).
            bool isAnd = combine == Expression.AndAlso;
            Expression? result = null;
            foreach (GlpiFilter item in items)
            {
                Expression? e = Translate(ctx, item, p);
                if (e is null)
                {
                    return null;
                }
                if (e is ConstantExpression { Value: bool constant })
                {
                    if (constant != isAnd)
                    {
                        return e;
                    }
                    continue;
                }
                result = result is null ? e : combine(result, e);
            }
            return result ?? Expression.Constant(isAnd);
        }
    }

    private Expression? TranslateId(GlpiDataContext ctx, Expression body, GlpiOp op, object? value)
    {
        object? Map(object? v) => GlpiValue.TryToLong(v, out long l) ? IdFromApi(ctx, (int)l) : v;
        object? mapped = value is System.Collections.IEnumerable list and not string
            ? list.Cast<object?>().Select(Map).ToList()
            : Map(value);
        return TranslateDefault(ctx, body, op, mapped, isUtcDate: false);
    }

    /// <summary>Comparaison SQL entre une expression du modèle et une valeur GLPI.</summary>
    public static Expression? TranslateDefault(GlpiDataContext ctx, Expression body, GlpiOp op, object? value, bool isUtcDate)
    {
        Type type = body.Type;
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        bool canBeNull = !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

        switch (op)
        {
            case GlpiOp.IsNull:
                return canBeNull ? Expression.Equal(body, Expression.Constant(null, type)) : Expression.Constant(false);
            case GlpiOp.NotNull:
                return canBeNull ? Expression.NotEqual(body, Expression.Constant(null, type)) : Expression.Constant(true);

            case GlpiOp.Like:
            case GlpiOp.NotLike:
            {
                Expression? text = AsText(body);
                if (text is null)
                {
                    return null;
                }
                string pattern = (GlpiValue.ToText(value) ?? string.Empty).ToLowerInvariant();
                Expression like = Expression.Call(
                    typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string)])!,
                    Expression.Constant(EF.Functions),
                    Expression.Call(text, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!),
                    Param(pattern));
                if (op == GlpiOp.Like)
                {
                    return Expression.AndAlso(Expression.NotEqual(text, Expression.Constant(null, typeof(string))), like);
                }
                return Expression.AndAlso(Expression.NotEqual(text, Expression.Constant(null, typeof(string))), Expression.Not(like));
            }

            case GlpiOp.In:
            case GlpiOp.NotIn:
            {
                if (value is not System.Collections.IEnumerable items || value is string)
                {
                    return null;
                }
                System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type))!;
                bool hasNull = false;
                foreach (object? item in items)
                {
                    if (item is null)
                    {
                        hasNull = true;
                        continue;
                    }
                    if (TryConvertValue(ctx, item, type, isUtcDate, out object? converted))
                    {
                        list.Add(converted);
                    }
                }
                Expression contains = Expression.Call(
                    typeof(Enumerable), nameof(Enumerable.Contains), [type],
                    ParamOf(list, typeof(List<>).MakeGenericType(type)), body);
                if (hasNull && canBeNull)
                {
                    contains = Expression.OrElse(contains, Expression.Equal(body, Expression.Constant(null, type)));
                }
                return op == GlpiOp.In ? contains : Expression.Not(contains);
            }
        }

        if (value is null)
        {
            return op switch
            {
                GlpiOp.Eq => canBeNull ? Expression.Equal(body, Expression.Constant(null, type)) : Expression.Constant(false),
                GlpiOp.Ne => canBeNull ? Expression.NotEqual(body, Expression.Constant(null, type)) : Expression.Constant(true),
                _ => Expression.Constant(false),
            };
        }

        if (!TryConvertValue(ctx, value, type, isUtcDate, out object? typed))
        {
            // Valeur sans sens pour la colonne (texte comparé à un entier...) : MySQL ne trouverait rien.
            return Expression.Constant(op == GlpiOp.Ne);
        }

        Expression constant = ParamOf(typed, type);

        if (underlying == typeof(string) && op is not (GlpiOp.Eq or GlpiOp.Ne))
        {
            // Comparaison d'ordre sur du texte : string.Compare est traduit par les trois fournisseurs.
            Expression compare = Expression.Call(typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!, body, constant);
            Expression zero = Expression.Constant(0);
            return op switch
            {
                GlpiOp.Lt => Expression.LessThan(compare, zero),
                GlpiOp.Le => Expression.LessThanOrEqual(compare, zero),
                GlpiOp.Gt => Expression.GreaterThan(compare, zero),
                GlpiOp.Ge => Expression.GreaterThanOrEqual(compare, zero),
                _ => null,
            };
        }

        if (underlying == typeof(bool) && op is not (GlpiOp.Eq or GlpiOp.Ne))
        {
            return null;
        }

        Expression left = body;
        if (underlying.IsEnum && op is not (GlpiOp.Eq or GlpiOp.Ne))
        {
            // Comparaison d'ordre sur une énumération : sur sa valeur entière.
            Type intType = Nullable.GetUnderlyingType(type) is null ? typeof(int) : typeof(int?);
            left = Expression.Convert(body, intType);
            constant = Expression.Convert(constant, intType);
        }

        return op switch
        {
            GlpiOp.Eq => Expression.Equal(left, constant),
            GlpiOp.Ne => canBeNull
                ? Expression.OrElse(Expression.Equal(left, Expression.Constant(null, left.Type)), Expression.NotEqual(left, constant))
                : Expression.NotEqual(left, constant),
            GlpiOp.Lt => Expression.LessThan(left, constant),
            GlpiOp.Le => Expression.LessThanOrEqual(left, constant),
            GlpiOp.Gt => Expression.GreaterThan(left, constant),
            GlpiOp.Ge => Expression.GreaterThanOrEqual(left, constant),
            _ => null,
        };
    }

    private static bool TryConvertValue(GlpiDataContext ctx, object? value, Type type, bool isUtcDate, out object? result)
    {
        if (!GlpiValue.TryConvert(value, type, out result))
        {
            return false;
        }
        if (isUtcDate && result is DateTime dt)
        {
            result = ctx.FromUserTime(dt);
        }
        return true;
    }

    /// <summary>Représentation textuelle SQL d'une expression, pour LIKE ; null si le type ne s'y prête pas.</summary>
    private static Expression? AsText(Expression body)
    {
        Type underlying = Nullable.GetUnderlyingType(body.Type) ?? body.Type;
        if (underlying == typeof(string))
        {
            return body;
        }
        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(decimal))
        {
            // Une colonne numérique NULL donnerait une exception côté client si ToString était évalué
            // en mémoire ; ici la méthode est traduite (CAST ... AS varchar).
            Expression value = Nullable.GetUnderlyingType(body.Type) is null ? body : Expression.Property(body, "Value");
            Expression text = Expression.Call(value, underlying.GetMethod(nameof(ToString), Type.EmptyTypes)!);
            return Nullable.GetUnderlyingType(body.Type) is null
                ? text
                : Expression.Condition(Expression.Property(body, "HasValue"), text, Expression.Constant(null, typeof(string)));
        }
        return null;
    }

    private static Expression Param(string value) => ParamOf(value, typeof(string));

    private static Expression ParamOf(object? value, Type type)
    {
        Type holderType = typeof(GlpiParam<>).MakeGenericType(type);
        object holder = Activator.CreateInstance(holderType, value)!;
        return Expression.Property(Expression.Constant(holder), "Value");
    }

    private static Expression<Func<T, bool>> BuildContains(Expression<Func<T, int>> key, List<int> ids)
    {
        ParameterExpression p = key.Parameters[0];
        Expression call = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(int)], ParamOf(ids, typeof(List<int>)), key.Body);
        return Expression.Lambda<Func<T, bool>>(call, p);
    }

    public static Expression Replace(LambdaExpression lambda, ParameterExpression p)
        => new ParameterReplacer(lambda.Parameters[0], p).Visit(lambda.Body);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    // --- Écriture ---------------------------------------------------------------------------

    public async Task<int> InsertAsync(GlpiDataContext ctx, GlpiInput input, CancellationToken ct)
    {
        if (!IsWritable)
        {
            throw GlpiWriteException.Right();
        }

        T entity = (T)Activator.CreateInstance(typeof(T))!;
        GlpiModelDefaults.FillRequiredStrings(entity);
        foreach (Action<T, GlpiDataContext> action in _onCreate)
        {
            action(entity, ctx);
        }

        // GLPI rattache à l'entité active un objet créé sans entities_id.
        GlpiInput effective = new(input);
        if (_columns.TryGetValue("entities_id", out GlpiColumnMap<T>? entityColumn) && entityColumn.Write is not null && !effective.ContainsKey("entities_id"))
        {
            effective["entities_id"] = ctx.ActiveApiEntity;
        }

        await PreloadLookupsAsync(ctx, effective.Keys, ct);
        Apply(entity, effective, ctx, isCreate: true);
        foreach (Func<T, GlpiDataContext, GlpiInput, CancellationToken, Task> action in _beforeSave)
        {
            await action(entity, ctx, effective, ct);
        }
        foreach (Action<T, GlpiDataContext> action in _onSave)
        {
            action(entity, ctx);
        }
        CheckEntityRight(ctx, entity);

        ctx.Db.Set<T>().Add(entity);
        await SaveAsync(ctx, ct);
        return IdToApi(ctx, _idGetter(entity));
    }

    public async Task UpdateAsync(GlpiDataContext ctx, int id, GlpiInput input, CancellationToken ct)
    {
        T entity = await FindTrackedAsync(ctx, id, ct) ?? throw new GlpiWriteException("Item not found");
        if (!IsWritable)
        {
            throw GlpiWriteException.Right();
        }

        GlpiInput effective = new(input.Where(kv => kv.Key != "id").ToDictionary(kv => kv.Key, kv => kv.Value));
        await PreloadLookupsAsync(ctx, effective.Keys, ct);
        Apply(entity, effective, ctx, isCreate: false);
        foreach (Func<T, GlpiDataContext, GlpiInput, CancellationToken, Task> action in _beforeSave)
        {
            await action(entity, ctx, effective, ct);
        }
        foreach (Action<T, GlpiDataContext> action in _onSave)
        {
            action(entity, ctx);
        }
        CheckEntityRight(ctx, entity);
        await SaveAsync(ctx, ct);
    }

    public async Task DeleteAsync(GlpiDataContext ctx, int id, bool purge, CancellationToken ct)
    {
        T entity = await FindTrackedAsync(ctx, id, ct) ?? throw new GlpiWriteException("Item not found");
        if (!IsWritable)
        {
            throw GlpiWriteException.Right();
        }
        CheckEntityRight(ctx, entity);

        if (!purge && TrashFlag is not null)
        {
            TrashFlag.SetValue(entity, true);
            foreach (Action<T, GlpiDataContext> action in _onSave)
            {
                action(entity, ctx);
            }
        }
        else
        {
            ctx.Db.Set<T>().Remove(entity);
        }
        await SaveAsync(ctx, ct);
    }

    public async Task RestoreAsync(GlpiDataContext ctx, int id, CancellationToken ct)
    {
        T entity = await FindTrackedAsync(ctx, id, ct) ?? throw new GlpiWriteException("Item not found");
        if (TrashFlag is null)
        {
            return;
        }
        CheckEntityRight(ctx, entity);
        TrashFlag.SetValue(entity, false);
        await SaveAsync(ctx, ct);
    }

    private async Task<T?> FindTrackedAsync(GlpiDataContext ctx, int apiId, CancellationToken ct)
    {
        Expression<Func<T, bool>> byId = BuildContains(_idExpr, [IdFromApi(ctx, apiId)]);
        T? entity = await BaseQuery(ctx, tracking: true).FirstOrDefaultAsync(byId, ct);
        if (entity is not null && RowFilter is not null)
        {
            foreach (Func<GlpiDataContext, CancellationToken, Task> preload in _preloads)
            {
                await preload(ctx, ct);
            }
            if (!RowFilter(entity, ctx))
            {
                return null;
            }
        }
        return entity;
    }

    private void Apply(T entity, GlpiInput input, GlpiDataContext ctx, bool isCreate)
    {
        foreach ((string key, System.Text.Json.Nodes.JsonNode? node) in input)
        {
            if (key == "id" || key.StartsWith('_') || !_columns.TryGetValue(key, out GlpiColumnMap<T>? column) || column.Write is null)
            {
                // Champs inconnus ou calculés : GLPI les ignore de la même façon (CommonDBTM::add
                // ne retient que les colonnes de la table).
                continue;
            }
            column.Write(entity, GlpiValue.FromJson(node), ctx);
        }
    }

    /// <summary>
    /// On ne peut rattacher un objet qu'à une entité active — comme <c>CommonDBTM::can(CREATE)</c>,
    /// qui vérifie <c>entities_id</c> dans <c>glpiactiveentities</c>.
    /// </summary>
    private static void CheckEntityRight(GlpiDataContext ctx, T entity)
    {
        if (entity is IEntityScoped scoped && !ctx.Scope.IsUnrestricted
            && scoped.EntityId is int entityId && Array.IndexOf(ctx.Scope.VisibleEntityIds, entityId) < 0)
        {
            throw GlpiWriteException.Right();
        }
    }

    private async Task SaveAsync(GlpiDataContext ctx, CancellationToken ct)
    {
        try
        {
            await ctx.Db.SaveChangesAsync(ct);
            foreach (Action<GlpiDataContext> action in _afterSave)
            {
                action(ctx);
            }
        }
        catch (UnauthorizedAccessException)
        {
            ctx.Db.ChangeTracker.Clear();
            throw GlpiWriteException.Right();
        }
        catch (DbUpdateException e)
        {
            ctx.Db.ChangeTracker.Clear();
            throw new GlpiWriteException(e.InnerException?.Message ?? e.Message);
        }
    }
}

/// <summary>Initialisation d'une entité neuve : les chaînes non nullables reçoivent "" plutôt que null.</summary>
internal static class GlpiModelDefaults
{
    private static readonly Dictionary<Type, PropertyInfo[]> RequiredStrings = [];
    private static readonly NullabilityInfoContext Nullability = new();

    public static void FillRequiredStrings(object entity)
    {
        PropertyInfo[] props;
        lock (RequiredStrings)
        {
            if (!RequiredStrings.TryGetValue(entity.GetType(), out props!))
            {
                props = [.. entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType == typeof(string) && p.CanWrite
                                && Nullability.Create(p).WriteState == NullabilityState.NotNull)];
                RequiredStrings[entity.GetType()] = props;
            }
        }
        foreach (PropertyInfo prop in props)
        {
            if (prop.GetValue(entity) is null)
            {
                prop.SetValue(entity, string.Empty);
            }
        }
    }
}
