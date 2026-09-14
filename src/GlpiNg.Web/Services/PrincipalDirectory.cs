using GlpiNg.Modules.Abstractions.Deployment;
using GlpiNg.Modules.Abstractions.Directory;
using GlpiNg.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Implémentation hôte de <see cref="IPrincipalDirectory"/> (annuaire des entités, groupes,
/// profils et utilisateurs proposés aux modules) — voir sa doc XML.
///
/// Rend aussi <see cref="IDeploymentTargetDirectory"/>, le contrat plus ancien et plus étroit du
/// seul module Déploiement, en déléguant aux mêmes requêtes : deux services distincts auraient
/// signifié deux fois les mêmes quatre requêtes, avec la garantie qu'elles divergent un jour.
/// </summary>
public sealed class PrincipalDirectory(IDbContextFactory<GlpiNgDbContext> dbFactory) : IPrincipalDirectory, IDeploymentTargetDirectory
{
    public async Task<IReadOnlyList<PrincipalOption>> GetAsync(PrincipalKind kind, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return kind switch
        {
            PrincipalKind.Entity => await db.Entities
                .AsNoTracking()
                .OrderBy(entity => entity.Name)
                .Select(entity => new PrincipalOption(entity.Id, entity.Name))
                .ToListAsync(cancellationToken),

            PrincipalKind.Group => await db.Groups
                .AsNoTracking()
                .OrderBy(group => group.Name)
                .Select(group => new PrincipalOption(group.Id, group.Name))
                .ToListAsync(cancellationToken),

            PrincipalKind.Profile => await db.Profiles
                .AsNoTracking()
                .OrderBy(profile => profile.Name)
                .Select(profile => new PrincipalOption(profile.Id, profile.Name))
                .ToListAsync(cancellationToken),

            PrincipalKind.User => await db.Users
                .AsNoTracking()
                .OrderBy(user => user.UserName)
                .Select(user => new PrincipalOption(user.Id, user.DisplayName ?? user.UserName))
                .ToListAsync(cancellationToken),

            _ => [],
        };
    }

    public async Task<IReadOnlyList<DeploymentTargetOption>> GetEntitiesAsync(CancellationToken cancellationToken = default)
        => await AsTargetsAsync(PrincipalKind.Entity, cancellationToken);

    public async Task<IReadOnlyList<DeploymentTargetOption>> GetGroupsAsync(CancellationToken cancellationToken = default)
        => await AsTargetsAsync(PrincipalKind.Group, cancellationToken);

    public async Task<IReadOnlyList<DeploymentTargetOption>> GetProfilesAsync(CancellationToken cancellationToken = default)
        => await AsTargetsAsync(PrincipalKind.Profile, cancellationToken);

    public async Task<IReadOnlyList<DeploymentTargetOption>> GetUsersAsync(CancellationToken cancellationToken = default)
        => await AsTargetsAsync(PrincipalKind.User, cancellationToken);

    private async Task<IReadOnlyList<DeploymentTargetOption>> AsTargetsAsync(PrincipalKind kind, CancellationToken cancellationToken)
        => [.. (await GetAsync(kind, cancellationToken)).Select(option => new DeploymentTargetOption(option.Id, option.Name))];
}
