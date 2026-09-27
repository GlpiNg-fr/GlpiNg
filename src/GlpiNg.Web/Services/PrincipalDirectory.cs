using GlpiNg.Modules.Abstractions.Deployment;
using GlpiNg.Modules.Abstractions.Directory;
using GlpiNg.Modules.Abstractions.Preferences;
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
///
/// Les noms rendus suivent les préférences de l'utilisateur connecté : ordre Nom/Prénom et
/// identifiant affiché (« Afficher les ID »). C'est l'endroit par où passent toutes les listes de
/// choix d'acteurs des modules, donc celui où le réglage s'applique partout d'un coup.
/// </summary>
public sealed class PrincipalDirectory(
    IDbContextFactory<GlpiNgDbContext> dbFactory,
    IUserPreferences preferences) : IPrincipalDirectory, IDeploymentTargetDirectory
{
    public async Task<IReadOnlyList<PrincipalOption>> GetAsync(PrincipalKind kind, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        UserPreferenceValues display = await preferences.GetAsync(cancellationToken);

        List<PrincipalOption> options = kind switch
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

            // Nom complet construit en mémoire : l'ordre dépend de la préférence, et le tri doit
            // suivre ce qui s'affiche, pas l'identifiant de connexion.
            PrincipalKind.User => [.. (await db.Users
                    .AsNoTracking()
                    .Select(user => new { user.Id, user.FirstName, user.LastName, user.DisplayName, user.UserName })
                    .ToListAsync(cancellationToken))
                .Select(user => new PrincipalOption(user.Id,
                    display.PersonName(user.FirstName, user.LastName, user.DisplayName ?? user.UserName)))
                .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)],

            _ => [],
        };

        return display.ShowIds
            ? [.. options.Select(option => option with { Name = display.WithId(option.Name, option.Id) })]
            : options;
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
