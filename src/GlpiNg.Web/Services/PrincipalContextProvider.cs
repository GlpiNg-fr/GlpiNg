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

        // L'arbre entier est chargé d'un coup (deux colonnes, une table qui compte des dizaines de
        // lignes, pas des milliers) : le parcours ci-dessous remonte et descend les branches
        // plusieurs fois, ce qu'une requête récursive par entité paierait bien plus cher.
        var entities = await db.Entities
            .AsNoTracking()
            .Select(entity => new { entity.Id, entity.ParentId })
            .ToListAsync(cancellationToken);

        Dictionary<int, int?> parentOf = entities.ToDictionary(entity => entity.Id, entity => entity.ParentId);

        ILookup<int?, int> childrenOf = entities.ToLookup(entity => entity.ParentId, entity => entity.Id);

        HashSet<int> entityIds = [];

        foreach (GlpiUserProfile habilitation in user.Habilitations)
        {
            entityIds.Add(habilitation.EntityId);

            // Habilitation récursive : l'utilisateur est aussi chez les descendants. Sans cette
            // descente, une cible posée sur une sous-entité ne verrait jamais un utilisateur
            // habilité récursivement au-dessus d'elle.
            if (habilitation.IsRecursive)
            {
                AddDescendants(childrenOf, habilitation.EntityId, entityIds);
            }
        }

        return new PrincipalContext
        {
            UserId = user.Id,
            UserName = user.UserName,
            EntityIds = [.. entityIds],
            EntityAncestorIds = [.. AncestorsOf(parentOf, entityIds)],
            ProfileIds = [.. user.Habilitations.Select(h => h.ProfileId).Distinct()],
            GroupIds = [.. user.GroupMemberships.Select(gm => gm.GroupId).Distinct()],
        };
    }

    /// <summary>Ajoute à <paramref name="into"/> tous les descendants de <paramref name="entityId"/>.</summary>
    private static void AddDescendants(ILookup<int?, int> childrenOf, int entityId, HashSet<int> into)
    {
        Queue<int> pending = new([entityId]);

        while (pending.Count > 0)
        {
            foreach (int child in childrenOf[pending.Dequeue()])
            {
                // Le test d'insertion sert aussi de garde-fou : une hiérarchie accidentellement
                // cyclique ferait tourner la file indéfiniment.
                if (into.Add(child))
                {
                    pending.Enqueue(child);
                }
            }
        }
    }

    /// <summary>Ancêtres stricts de <paramref name="entityIds"/>, remontés de parent en parent.</summary>
    private static HashSet<int> AncestorsOf(Dictionary<int, int?> parentOf, HashSet<int> entityIds)
    {
        HashSet<int> ancestors = [];

        foreach (int entityId in entityIds)
        {
            int? current = parentOf.GetValueOrDefault(entityId);

            while (current is int parent && ancestors.Add(parent))
            {
                current = parentOf.GetValueOrDefault(parent);
            }
        }

        return ancestors;
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
