namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>
/// Table GLPI qui réunit plusieurs sources GlpiNg : GLPI range dans une même table ce que GlpiNg
/// porte en champs distincts — demandeur et technicien d'un ticket (<c>glpi_tickets_users</c>),
/// solution d'un ticket, d'un problème ou d'un changement (<c>glpi_itilsolutions</c>)...
///
/// L'identifiant GLPI encode la source : <c>id × nombre de sources + rang de la source</c>. Il est
/// stable (il ne dépend que de l'objet source), ce qui suffit à le relire.
///
/// ponytail: conditions, tris et pagination évalués en mémoire sur l'ensemble des lignes visibles —
/// adapté à des tables de liaison, à revoir (conditions poussées vers chaque source) si l'une
/// d'elles dépasse quelques dizaines de milliers de lignes.
/// </summary>
public sealed class UnionGlpiTable(string name, params IGlpiTable[] parts) : IGlpiTable
{
    private readonly IGlpiTable[] _parts = parts;

    public string Name { get; } = name;

    public Type? ClrType => _parts.Select(p => p.ClrType).FirstOrDefault(t => t is not null);

    public bool IsWritable => false;

    public bool HasTrash => false;

    public bool IsMapped(string column) => _parts.Any(p => p.IsMapped(column));

    private int Encode(int partIndex, int id) => id * _parts.Length + partIndex;

    private (int Part, int Id) Decode(int id) => (id % _parts.Length, id / _parts.Length);

    private async Task<List<GlpiRow>> AllRowsAsync(GlpiDataContext ctx, CancellationToken ct, GlpiFilter? filter = null)
    {
        // Une condition qui ne porte pas sur l'identifiant (encodé ici) est confiée à chaque source :
        // les éléments d'un objet (items_id = 12) ne chargent que les siens.
        GlpiFilter? pushed = filter is not null && !filter.Columns().Contains("id") ? filter : null;
        List<GlpiRow> rows = [];
        for (int i = 0; i < _parts.Length; i++)
        {
            GlpiPage page = await _parts[i].QueryAsync(ctx, pushed, null, 0, null, ct);
            Dictionary<int, GlpiRow> partRows = await _parts[i].GetRowsAsync(ctx, page.Ids, ct);
            foreach (GlpiRow row in partRows.Values)
            {
                row["id"] = (long)Encode(i, row.Id);
                rows.Add(row);
            }
        }
        return rows;
    }

    public async Task<GlpiPage> QueryAsync(GlpiDataContext ctx, GlpiFilter? filter, IReadOnlyList<GlpiSort>? sort, int start, int? limit, CancellationToken ct)
    {
        IEnumerable<GlpiRow> rows = (await AllRowsAsync(ctx, ct, filter)).Where(r => GlpiFilterEvaluator.Matches(r, filter));
        IComparer<object?> comparer = Comparer<object?>.Create(GlpiValue.Compare);
        IOrderedEnumerable<GlpiRow> ordered = rows.OrderBy(r => 0);
        foreach (GlpiSort s in sort ?? [])
        {
            ordered = s.Descending
                ? ordered.ThenByDescending(r => r.GetValueOrDefault(s.Column), comparer)
                : ordered.ThenBy(r => r.GetValueOrDefault(s.Column), comparer);
        }
        List<GlpiRow> all = [.. ordered.ThenBy(r => r.Id)];
        IEnumerable<GlpiRow> paged = all.Skip(start);
        if (limit is int l)
        {
            paged = paged.Take(l);
        }
        return new GlpiPage([.. paged.Select(r => r.Id)], all.Count);
    }

    public async Task<Dictionary<int, GlpiRow>> GetRowsAsync(GlpiDataContext ctx, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        Dictionary<int, GlpiRow> result = [];
        foreach (IGrouping<int, (int Part, int Id)> group in ids.Select(Decode).GroupBy(d => d.Part))
        {
            Dictionary<int, GlpiRow> rows = await _parts[group.Key].GetRowsAsync(ctx, [.. group.Select(g => g.Id)], ct);
            foreach (GlpiRow row in rows.Values)
            {
                int encoded = Encode(group.Key, row.Id);
                row["id"] = (long)encoded;
                result[encoded] = row;
            }
        }
        return result;
    }

    public async Task<Dictionary<int, object?>> GetColumnAsync(GlpiDataContext ctx, string column, GlpiFilter? filter, CancellationToken ct)
        => (await AllRowsAsync(ctx, ct, filter))
            .Where(r => GlpiFilterEvaluator.Matches(r, filter))
            .ToDictionary(r => r.Id, r => r.GetValueOrDefault(column));

    public Task<int> InsertAsync(GlpiDataContext ctx, GlpiInput input, CancellationToken ct) => throw GlpiWriteException.Right();

    public Task UpdateAsync(GlpiDataContext ctx, int id, GlpiInput input, CancellationToken ct) => throw GlpiWriteException.Right();

    public Task DeleteAsync(GlpiDataContext ctx, int id, bool purge, CancellationToken ct) => throw GlpiWriteException.Right();

    public Task RestoreAsync(GlpiDataContext ctx, int id, CancellationToken ct) => throw GlpiWriteException.Right();
}
