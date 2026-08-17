using GlpiNg.Modules.Abstractions.Deployment;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>Implémentation hôte de <see cref="ICurrentUserDeploymentContextProvider"/> — voir sa doc XML.</summary>
public sealed class CurrentUserDeploymentContextProvider(IDbContextFactory<GlpiNgDbContext> dbFactory) : ICurrentUserDeploymentContextProvider
{
    public async Task<CurrentUserDeploymentContext?> GetContextAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        GlpiUser? user = await db.Users
            .AsNoTracking()
            .Include(u => u.Habilitations)
            .Include(u => u.GroupMemberships)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        return new CurrentUserDeploymentContext
        {
            UserId = user.Id,
            UserName = user.UserName,
            EntityIds = user.Habilitations.Select(h => h.EntityId).Distinct().ToList(),
            ProfileIds = user.Habilitations.Select(h => h.ProfileId).Distinct().ToList(),
            GroupIds = user.GroupMemberships.Select(gm => gm.GroupId).Distinct().ToList()
        };
    }
}
