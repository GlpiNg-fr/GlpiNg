using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Items;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Items;

/// <summary>
/// Voir <see cref="IItemRelations"/> : stocke les rattachements et laisse les modules porteurs des
/// objets (<see cref="IRelationTargetSource"/>) dire leur nom, leur adresse et ce qu'on a le droit d'en voir.
/// </summary>
public sealed class ItemRelationService(
    IDbContextFactory<GlpiNgDbContext> dbFactory,
    IEnumerable<IRelationTargetSource> sources) : IItemRelations
{
    /// <summary>Types d'objets du parc qu'une analyse d'impact peut désigner.</summary>
    private static readonly string[] ImpactTypes =
    [
        ItemTypes.Computer, ItemTypes.Monitor, ItemTypes.Peripheral, ItemTypes.Printer, ItemTypes.Phone,
        ItemTypes.NetworkEquipment, ItemTypes.Rack, ItemTypes.Enclosure, ItemTypes.Pdu, ItemTypes.PassiveEquipment,
        ItemTypes.Cable, ItemTypes.Appliance, ItemTypes.Cluster, ItemTypes.Datacenter,
        ItemTypes.DatabaseInstance, ItemTypes.Domain, ItemTypes.Certificate,
    ];

    private static readonly Dictionary<string, string[]> Types = new()
    {
        [ItemRelationKinds.Certificate] = [ItemTypes.Certificate],
        [ItemRelationKinds.Domain] = [ItemTypes.Domain],
        [ItemRelationKinds.Appliance] = [ItemTypes.Appliance],
        [ItemRelationKinds.Project] = [ItemTypes.Project],
        [ItemRelationKinds.Ticket] = [ItemTypes.Ticket],
        [ItemRelationKinds.Problem] = [ItemTypes.Problem],
        [ItemRelationKinds.Change] = [ItemTypes.Change],
        [ItemRelationKinds.Knowledge] = [ItemTypes.KnowledgeBaseArticle],
        [ItemRelationKinds.Line] = [ItemTypes.PhoneLine],
        [ItemRelationKinds.Database] = [ItemTypes.DatabaseInstance],
        [ItemRelationKinds.Rack] = [ItemTypes.Rack],
        [ItemRelationKinds.Enclosure] = [ItemTypes.Enclosure],
        [ItemRelationKinds.Pdu] = [ItemTypes.Pdu],
        [ItemRelationKinds.License] = [ItemTypes.SoftwareLicense],
        [ItemRelationKinds.Printer] = [ItemTypes.Printer],
        [ItemRelationKinds.Impact] = ImpactTypes,
    };

    public IReadOnlyCollection<string> TargetTypesOf(string relation) =>
        Types.TryGetValue(relation, out string[]? types) ? types : [];

    public async Task<IReadOnlyList<ItemRelationLink>> GetAsync(string itemType, int itemId, string relation, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<ItemRelation> forward = await db.ItemRelations.AsNoTracking()
            .Where(r => r.Relation == relation && r.ItemType == itemType && r.ItemId == itemId)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        List<ItemRelationLink> links = await ResolveAsync(forward.Select(r => (r.Id, r.TargetType, r.TargetId)), false, cancellationToken);

        // L'analyse d'impact se lit dans les deux sens : ce que l'objet impacte, et ce qui l'impacte.
        if (relation == ItemRelationKinds.Impact)
        {
            List<ItemRelation> reverse = await db.ItemRelations.AsNoTracking()
                .Where(r => r.Relation == relation && r.TargetType == itemType && r.TargetId == itemId)
                .OrderBy(r => r.Id)
                .ToListAsync(cancellationToken);

            links.AddRange(await ResolveAsync(reverse.Select(r => (r.Id, r.ItemType, r.ItemId)), true, cancellationToken));
        }

        return links;
    }

    public async Task<IReadOnlyList<RelationTarget>> SearchTargetsAsync(string itemType, int itemId, string relation, string? term, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        HashSet<(string, int)> taken = [.. (await db.ItemRelations.AsNoTracking()
            .Where(r => r.Relation == relation && r.ItemType == itemType && r.ItemId == itemId)
            .Select(r => new { r.TargetType, r.TargetId }).ToListAsync(cancellationToken))
            .Select(r => (r.TargetType, r.TargetId))];

        List<RelationTarget> found = [];
        foreach (string type in TargetTypesOf(relation))
        {
            foreach (IRelationTargetSource source in sources.Where(s => s.SupportedTypes.Contains(type)))
            {
                found.AddRange(await source.SearchAsync(type, term, 25, cancellationToken));
            }
        }

        // Un objet ne s'impacte pas lui-même.
        return [.. found.Where(t => !taken.Contains((t.ItemType, t.ItemId)) && !(t.ItemType == itemType && t.ItemId == itemId))];
    }

    public async Task<IReadOnlyList<ItemRelationLink>> GetIncomingAsync(string targetType, int targetId, string relation, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<ItemRelation> rows = await db.ItemRelations.AsNoTracking()
            .Where(r => r.Relation == relation && r.TargetType == targetType && r.TargetId == targetId)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return await ResolveAsync(rows.Select(r => (r.Id, r.ItemType, r.ItemId)), false, cancellationToken);
    }

    public async Task<IReadOnlyList<RelationTarget>> SearchIncomingAsync(string targetType, int targetId, string relation, string? term, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        HashSet<(string, int)> taken = [.. (await db.ItemRelations.AsNoTracking()
            .Where(r => r.Relation == relation && r.TargetType == targetType && r.TargetId == targetId)
            .Select(r => new { r.ItemType, r.ItemId }).ToListAsync(cancellationToken))
            .Select(r => (r.ItemType, r.ItemId))];

        List<RelationTarget> found = [];
        foreach (string type in ImpactTypes.Where(t => t != ItemTypes.Appliance))
        {
            foreach (IRelationTargetSource source in sources.Where(s => s.SupportedTypes.Contains(type)))
            {
                found.AddRange(await source.SearchAsync(type, term, 25, cancellationToken));
            }
        }

        return [.. found.Where(t => !taken.Contains((t.ItemType, t.ItemId)))];
    }

    public async Task AddAsync(string itemType, int itemId, string relation, string targetType, int targetId, CancellationToken cancellationToken = default)
    {
        if (!TargetTypesOf(relation).Contains(targetType))
        {
            return;
        }

        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.ItemRelations.AnyAsync(r => r.Relation == relation && r.ItemType == itemType && r.ItemId == itemId
                && r.TargetType == targetType && r.TargetId == targetId, cancellationToken))
        {
            return;
        }

        db.ItemRelations.Add(new ItemRelation { ItemType = itemType, ItemId = itemId, Relation = relation, TargetType = targetType, TargetId = targetId });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int linkId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.ItemRelations.Where(r => r.Id == linkId).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<List<ItemRelationLink>> ResolveAsync(IEnumerable<(int LinkId, string Type, int Id)> rows, bool reverse, CancellationToken ct)
    {
        List<ItemRelationLink> links = [];

        foreach (IGrouping<string, (int LinkId, string Type, int Id)> group in rows.GroupBy(r => r.Type))
        {
            Dictionary<int, RelationTarget> resolved = [];
            foreach (IRelationTargetSource source in sources.Where(s => s.SupportedTypes.Contains(group.Key)))
            {
                foreach (RelationTarget target in await source.ResolveAsync(group.Key, [.. group.Select(g => g.Id)], ct))
                {
                    resolved[target.ItemId] = target;
                }
            }

            links.AddRange(group.Where(g => resolved.ContainsKey(g.Id)).Select(g => new ItemRelationLink(g.LinkId, resolved[g.Id], reverse)));
        }

        return [.. links.OrderBy(l => l.Target.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
