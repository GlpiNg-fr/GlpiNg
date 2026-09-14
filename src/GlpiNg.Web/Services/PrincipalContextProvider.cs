using GlpiNg.Modules.Abstractions.Deployment;
using GlpiNg.Modules.Abstractions.Directory;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Implémentation hôte de <see cref="IPrincipalContextProvider"/> (habilitations d'un utilisateur,
/// pour évaluer les cibles d'un objet) — voir sa doc XML.
///
/// Rend aussi <see cref="ICurrentUserDeploymentContextProvider"/>, le contrat plus ancien du seul
/// module Déploiement, à partir de la même lecture — même raison que
/// <see cref="PrincipalDirectory"/>.
/// </summary>
public sealed class PrincipalContextProvider(IDbContextFactory<GlpiNgDbContext> dbFactory)
    : IPrincipalContextProvider, ICurrentUserDeploymentContextProvider
{
    public async Task<PrincipalContext?> GetAsync(int userId, CancellationToken cancellationToken = default)
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

        return new PrincipalContext
        {
            UserId = user.Id,
            UserName = user.UserName,
            EntityIds = [.. user.Habilitations.Select(h => h.EntityId).Distinct()],
            ProfileIds = [.. user.Habilitations.Select(h => h.ProfileId).Distinct()],
            GroupIds = [.. user.GroupMemberships.Select(gm => gm.GroupId).Distinct()],
        };
    }

    public async Task<CurrentUserDeploymentContext?> GetContextAsync(int userId, CancellationToken cancellationToken = default)
    {
        PrincipalContext? context = await GetAsync(userId, cancellationToken);

        return context is null
            ? null
            : new CurrentUserDeploymentContext
            {
                UserId = context.UserId,
                UserName = context.UserName,
                EntityIds = context.EntityIds,
                ProfileIds = context.ProfileIds,
                GroupIds = context.GroupIds,
            };
    }
}
