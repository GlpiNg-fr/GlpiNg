using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Management.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Items;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Items;

/// <summary>Voir <see cref="IItemManagement"/>. Une ligne par objet, créée à la première saisie.</summary>
public sealed class ItemManagementService(
    IDbContextFactory<GlpiNgDbContext> dbFactory,
    IEnumerable<IRelationTargetSource> sources) : IItemManagement
{
    public async Task<ItemManagementData> GetAsync(string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        ItemManagement? row = await db.ItemManagements.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ItemType == itemType && m.ItemId == itemId, cancellationToken);

        return row is null
            ? new ItemManagementData(null, null, null, null, null, null, null, null, null, null)
            : new ItemManagementData(row.SupplierId, row.BudgetId, row.PurchaseDate, row.DeliveryDate, row.UseDate,
                row.WarrantyMonths, row.WarrantyInfo, row.Value, row.OrderNumber, row.DeliveryNumber);
    }

    public async Task SaveAsync(string itemType, int itemId, ItemManagementData data, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        ItemManagement? row = await db.ItemManagements.FirstOrDefaultAsync(m => m.ItemType == itemType && m.ItemId == itemId, cancellationToken);
        if (row is null)
        {
            row = new ItemManagement { ItemType = itemType, ItemId = itemId };
            db.ItemManagements.Add(row);
        }

        row.SupplierId = data.SupplierId;
        row.BudgetId = data.BudgetId;
        row.PurchaseDate = data.PurchaseDate;
        row.DeliveryDate = data.DeliveryDate;
        row.UseDate = data.UseDate;
        row.WarrantyMonths = data.WarrantyMonths;
        row.WarrantyInfo = data.WarrantyInfo;
        row.Value = data.Value;
        row.OrderNumber = data.OrderNumber;
        row.DeliveryNumber = data.DeliveryNumber;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManagementOption>> GetSuppliersAsync(CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return [.. (await db.Set<Supplier>().AsNoTracking().OrderBy(s => s.Name).Select(s => new { s.Id, s.Name }).ToListAsync(cancellationToken))
            .Select(s => new ManagementOption(s.Id, s.Name))];
    }

    public async Task<IReadOnlyList<ManagementOption>> GetBudgetsAsync(CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return [.. (await db.Set<Budget>().AsNoTracking().OrderBy(b => b.Name).Select(b => new { b.Id, b.Name }).ToListAsync(cancellationToken))
            .Select(b => new ManagementOption(b.Id, b.Name))];
    }

    public async Task<IReadOnlyList<RelationTarget>> GetItemsAsync(string kind, int id, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<ItemManagement> rows = db.ItemManagements.AsNoTracking();
        rows = kind == "budget" ? rows.Where(m => m.BudgetId == id) : rows.Where(m => m.SupplierId == id);

        var found = await rows.Select(m => new { m.ItemType, m.ItemId }).ToListAsync(cancellationToken);

        List<RelationTarget> targets = [];
        foreach (var group in found.GroupBy(r => r.ItemType))
        {
            foreach (IRelationTargetSource source in sources.Where(s => s.SupportedTypes.Contains(group.Key)))
            {
                targets.AddRange(await source.ResolveAsync(group.Key, [.. group.Select(g => g.ItemId)], cancellationToken));
            }
        }

        return [.. targets.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)];
    }
}
