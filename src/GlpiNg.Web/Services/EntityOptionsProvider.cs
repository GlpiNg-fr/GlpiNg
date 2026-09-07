using GlpiNg.Modules.Abstractions.Entities;
using GlpiNg.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Implémentation hôte d'<see cref="IEntityOptionsProvider"/>. La liste est bornée au
/// cloisonnement courant : un utilisateur restreint ne se voit proposer que les entités qu'il voit
/// déjà, pour ne pas pouvoir expédier un objet hors de sa propre portée.
/// </summary>
public sealed class EntityOptionsProvider(
    IRootDbContextFactory dbFactory,
    IEntityScopeProvider scopeProvider) : IEntityOptionsProvider
{
    public async Task<IReadOnlyList<EntityOption>> GetAssignableAsync(CancellationToken cancellationToken = default)
    {
        EntityScope scope = scopeProvider.Current;

        // Fabrique racine et filtrage explicite plutôt que le filtre global : GlpiEntity n'est pas
        // elle-même cloisonnée (elle n'implémente pas IEntityScoped), c'est donc ici que la
        // restriction doit être posée.
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Models.GlpiEntity> query = db.Entities.AsNoTracking();

        if (!scope.IsUnrestricted)
        {
            int[] visible = scope.VisibleEntityIds;
            query = query.Where(entity => visible.Contains(entity.Id));
        }

        return await query
            .OrderBy(entity => entity.Name)
            .Select(entity => new EntityOption(entity.Id, entity.Name))
            .ToListAsync(cancellationToken);
    }
}
