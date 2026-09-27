using System.Security.Claims;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>Une entité sur laquelle l'utilisateur est habilité, et si l'habilitation est récursive.</summary>
/// <param name="EntityId">Entité de l'habilitation (<see cref="GlpiUserProfile.EntityId"/>).</param>
/// <param name="Name">Nom de l'entité, pour l'affichage du sélecteur.</param>
/// <param name="IsRecursive">Vrai si l'habilitation porte aussi sur les sous-entités.</param>
public sealed record EntityHabilitation(int EntityId, string Name, bool IsRecursive);

/// <summary>
/// Traduit les habilitations d'un utilisateur (<see cref="GlpiUserProfile"/>) en entité active :
/// quelle entité proposer par défaut à la connexion, lesquelles il a le droit d'activer, et les
/// revendications à déposer dans le cookie d'authentification.
///
/// L'entité active est stockée dans le cookie plutôt que côté serveur : elle survit ainsi à une
/// reconnexion du circuit Blazor et reste lisible aussi bien depuis un contrôleur que depuis un
/// composant, sans état de session partagé. En changer repasse donc par une reconnexion du cookie
/// (voir <c>AccountController.SwitchEntity</c>), comme le changement d'entité de GLPI recharge la
/// page.
/// </summary>
public sealed class UserEntityAccessService(IRootDbContextFactory dbFactory)
{
    /// <summary>Entité active retenue pour la session, telle que déposée à la connexion.</summary>
    public const string ActiveEntityClaim = "glping:active_entity";

    /// <summary>« Voir aussi les sous-entités » de l'entité active ("1" ou "0").</summary>
    public const string RecursiveClaim = "glping:active_entity_recursive";

    /// <summary>
    /// Déposée sur toute session applicative : "1" pour un compte non cloisonné (administrateur),
    /// "0" sinon. Sa seule présence signale à <c>EntityScopeProvider</c> qu'il a affaire à une
    /// session issue du formulaire de connexion — une identité authentifiée qui ne la porte pas
    /// est un appel machine-à-machine (jeton OAuth), qui n'est pas cloisonné.
    /// </summary>
    public const string UnrestrictedClaim = "glping:entity_unrestricted";

    public async Task<List<EntityHabilitation>> GetHabilitationsAsync(int userId, CancellationToken ct = default)
    {
        await using GlpiNgDbContext db = dbFactory.CreateDbContext();

        // Distinct porte sur une projection de valeurs simples, puis le tri et la construction du
        // record se font en mémoire. Un Distinct suivi d'un OrderBy sur un type projeté n'est pas
        // traduisible par EF, et faisait échouer toute connexion d'un compte non administrateur —
        // les administrateurs court-circuitent cet appel (voir BuildScopeClaimsAsync).
        var rows = await db.UserProfiles
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .Select(h => new { h.EntityId, EntityName = h.Entity.Name, h.IsRecursive })
            .Distinct()
            .ToListAsync(ct);

        // Tri en mémoire, et sensible à la culture : ce sont des noms d'entités affichés tels quels
        // dans le sélecteur.
        return [.. rows
            .Select(row => new EntityHabilitation(row.EntityId, row.EntityName, row.IsRecursive))
            .OrderBy(habilitation => habilitation.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// Revendications de cloisonnement à ajouter à l'identité au moment de la connexion.
    /// Un administrateur reçoit <see cref="UnrestrictedClaim"/> : il n'est pas cloisonné, ce qui
    /// évite qu'une base sans habilitation cohérente (comptes importés d'une base GLPI, où les
    /// droits fins ne sont pas repris) ne laisse plus personne administrer l'application.
    /// </summary>
    public async Task<(List<Claim> Claims, int? ActiveEntityId)> BuildScopeClaimsAsync(GlpiUser user, CancellationToken ct = default)
    {
        if (user.IsAdmin)
        {
            return ([new Claim(UnrestrictedClaim, "1")], null);
        }

        List<EntityHabilitation> habilitations = await GetHabilitationsAsync(user.Id, ct);
        if (habilitations.Count == 0)
        {
            // Pas d'habilitation : le marqueur seul, sans entité active. EntityScopeProvider en
            // déduit EntityScope.None — l'utilisateur est connecté mais ne voit rien, plutôt que
            // de voir tout le parc.
            return ([new Claim(UnrestrictedClaim, "0")], null);
        }

        // Entité par défaut : la plus « haute » habilitation récursive si elle existe (elle couvre
        // le plus de choses), sinon la première par ordre alphabétique. GLPI reprend la dernière
        // entité active de l'utilisateur ; ce n'est pas mémorisé ici.
        EntityHabilitation preferred = habilitations.FirstOrDefault(h => h.IsRecursive) ?? habilitations[0];

        return (BuildScopeClaims(preferred.EntityId, preferred.IsRecursive), preferred.EntityId);
    }

    public static List<Claim> BuildScopeClaims(int activeEntityId, bool recursive) =>
    [
        new Claim(UnrestrictedClaim, "0"),
        new Claim(ActiveEntityClaim, activeEntityId.ToString()),
        new Claim(RecursiveClaim, recursive ? "1" : "0"),
    ];

    /// <summary>
    /// Vrai si l'utilisateur a le droit d'activer cette entité : elle doit être couverte par l'une
    /// de ses habilitations, directement ou par récursivité. Contrôlé côté serveur avant de
    /// réémettre le cookie — la valeur vient d'un formulaire, donc de l'utilisateur.
    /// </summary>
    public async Task<bool> CanActivateAsync(GlpiUser user, int entityId, EntityTreeCache tree, CancellationToken ct = default)
    {
        if (user.IsAdmin)
        {
            return true;
        }

        List<EntityHabilitation> habilitations = await GetHabilitationsAsync(user.Id, ct);

        return habilitations.Any(h => h.EntityId == entityId
                                      || (h.IsRecursive && tree.IsSelfOrDescendant(h.EntityId, entityId)));
    }

    /// <summary>
    /// Surcharge par identifiant, pour les composants qui n'ont que la revendication
    /// <see cref="ClaimTypes.NameIdentifier"/> sous la main.
    /// </summary>
    public async Task<List<EntityHabilitation>> GetActivatableForUserIdAsync(int userId, EntityTreeCache tree, CancellationToken ct = default)
    {
        await using GlpiNgDbContext db = dbFactory.CreateDbContext();
        GlpiUser? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

        return user is null ? [] : await GetActivatableAsync(user, tree, ct);
    }

    /// <summary>
    /// Entités que l'utilisateur peut activer : ses habilitations, plus les sous-entités de
    /// celles qui sont récursives. C'est ce que liste le sélecteur d'entité.
    /// </summary>
    public async Task<List<EntityHabilitation>> GetActivatableAsync(GlpiUser user, EntityTreeCache tree, CancellationToken ct = default)
    {
        Dictionary<int, string> names = await tree.GetNamesAsync(ct);

        if (user.IsAdmin)
        {
            return [.. names.Select(n => new EntityHabilitation(n.Key, n.Value, true)).OrderBy(h => h.Name)];
        }

        List<EntityHabilitation> habilitations = await GetHabilitationsAsync(user.Id, ct);
        Dictionary<int, bool> recursiveByEntityId = [];

        foreach (EntityHabilitation habilitation in habilitations)
        {
            recursiveByEntityId[habilitation.EntityId] = habilitation.IsRecursive;

            if (!habilitation.IsRecursive)
            {
                continue;
            }

            foreach (int descendantId in tree.GetSelfAndDescendants(habilitation.EntityId))
            {
                // Une sous-entité héritée reste activable en mode récursif : l'utilisateur peut
                // s'y restreindre puis redescendre.
                recursiveByEntityId.TryAdd(descendantId, true);
            }
        }

        return [.. recursiveByEntityId
            .Where(pair => names.ContainsKey(pair.Key))
            .Select(pair => new EntityHabilitation(pair.Key, names[pair.Key], pair.Value))
            .OrderBy(h => h.Name)];
    }
}
