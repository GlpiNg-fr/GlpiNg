using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Items;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Items;

/// <summary>Voir <see cref="IItemCosts"/>.</summary>
public sealed class ItemCostService(IDbContextFactory<GlpiNgDbContext> dbFactory) : IItemCosts
{
    public async Task<IReadOnlyList<ItemCostLine>> GetAsync(string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return [.. (await db.ItemCosts.AsNoTracking()
            .Where(c => c.ItemType == itemType && c.ItemId == itemId)
            .OrderByDescending(c => c.Date).ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken))
            .Select(c => new ItemCostLine(c.Id, c.Name, c.Date, c.Amount, c.Comment))];
    }

    public async Task AddAsync(string itemType, int itemId, string name, DateTime? date, decimal amount, string? comment, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.ItemCosts.Add(new ItemCost { ItemType = itemType, ItemId = itemId, Name = name.Trim(), Date = date, Amount = amount, Comment = comment });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int costId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.ItemCosts.Where(c => c.Id == costId).ExecuteDeleteAsync(cancellationToken);
    }
}
