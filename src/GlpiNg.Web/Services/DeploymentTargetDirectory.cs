using GlpiNg.Modules.Abstractions.Deployment;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>Implémentation hôte de <see cref="IDeploymentTargetDirectory"/> — voir sa doc XML.</summary>
public sealed class DeploymentTargetDirectory(IDbContextFactory<GlpiNgDbContext> dbFactory) : IDeploymentTargetDirectory
{
    public async Task<IReadOnlyList<DeploymentTargetOption>> GetEntitiesAsync(CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Entities
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .Select(entity => new DeploymentTargetOption(entity.Id, entity.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeploymentTargetOption>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Groups
            .AsNoTracking()
            .OrderBy(group => group.Name)
            .Select(group => new DeploymentTargetOption(group.Id, group.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeploymentTargetOption>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Profiles
            .AsNoTracking()
            .OrderBy(profile => profile.Name)
            .Select(profile => new DeploymentTargetOption(profile.Id, profile.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeploymentTargetOption>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .Select(user => new DeploymentTargetOption(user.Id, user.DisplayName ?? user.UserName))
            .ToListAsync(cancellationToken);
    }
}
