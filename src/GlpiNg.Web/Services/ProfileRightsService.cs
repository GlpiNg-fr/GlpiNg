using System.Security.Claims;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Résout les droits d'un utilisateur pour une entité donnée, et les traduit en revendications
/// déposées dans le cookie d'authentification — même mécanique que l'entité active (voir
/// <see cref="UserEntityAccessService"/>), pour la même raison : les droits sont alors lisibles
/// sans requête depuis un contrôleur comme depuis un composant.
///
/// Les droits dépendent de l'entité active : dans GLPI une habilitation associe un profil À une
/// entité. Un même utilisateur peut donc être gestionnaire du parc dans une entité et simple
/// lecteur dans une autre. Changer d'entité active recalcule donc les droits.
/// </summary>
public sealed class ProfileRightsService(IRootDbContextFactory dbFactory)
{
    /// <summary>Préfixe des revendications de droits : <c>glping:right:parc</c> = "0" | "1" | "2".</summary>
    public const string RightClaimPrefix = "glping:right:";

    public static string ClaimTypeFor(ProfileSection section) => RightClaimPrefix + section.ToString().ToLowerInvariant();

    /// <summary>
    /// Droits de l'utilisateur sur <paramref name="activeEntityId"/> : union (niveau le plus
    /// élevé, section par section) des profils de ses habilitations qui couvrent cette entité,
    /// directement ou par récursivité.
    /// </summary>
    public async Task<ProfileRights> ResolveAsync(GlpiUser user, int? activeEntityId, EntityTreeCache tree, CancellationToken ct = default)
    {
        if (user.IsAdmin)
        {
            return ProfileRights.Full;
        }

        if (activeEntityId is not int entityId)
        {
            return ProfileRights.None;
        }

        await using GlpiNgDbContext db = dbFactory.CreateDbContext();

        // Type anonyme plutôt qu'un ValueTuple construit explicitement : la projection est alors
        // d'une forme qu'EF sait traduire à coup sûr. Cette requête s'exécute à chaque connexion
        // d'un compte non administrateur, juste après celle des habilitations — elle n'a pas le
        // droit d'échouer à la traduction.
        var habilitations = await db.UserProfiles
            .AsNoTracking()
            .Where(h => h.UserId == user.Id)
            .Select(h => new { h.EntityId, h.IsRecursive, h.Profile })
            .ToListAsync(ct);

        List<GlpiProfile> applicable = [.. habilitations
            .Where(h => h.EntityId == entityId
                        || (h.IsRecursive && tree.IsSelfOrDescendant(h.EntityId, entityId)))
            .Select(h => h.Profile)];

        if (applicable.Count == 0)
        {
            return ProfileRights.None;
        }

        return new ProfileRights(section => applicable.Max(profile => LevelOf(profile, section)));
    }

    public async Task<List<Claim>> BuildRightClaimsAsync(GlpiUser user, int? activeEntityId, EntityTreeCache tree, CancellationToken ct = default)
    {
        ProfileRights rights = await ResolveAsync(user, activeEntityId, tree, ct);

        return [.. Enum.GetValues<ProfileSection>()
            .Select(section => new Claim(ClaimTypeFor(section), ((int)rights.Level(section)).ToString()))];
    }

    private static ProfileRightLevel LevelOf(GlpiProfile profile, ProfileSection section) => section switch
    {
        ProfileSection.Parc => profile.ParcRight,
        ProfileSection.Assistance => profile.AssistanceRight,
        ProfileSection.Gestion => profile.GestionRight,
        ProfileSection.Outils => profile.OutilsRight,
        ProfileSection.Administration => profile.AdministrationRight,
        ProfileSection.Configuration => profile.ConfigurationRight,
        _ => ProfileRightLevel.None,
    };
}
