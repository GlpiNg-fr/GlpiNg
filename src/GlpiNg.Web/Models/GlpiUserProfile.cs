namespace GlpiNg.Web.Models;

/// <summary>
/// Habilitation GLPI : une combinaison utilisateur + entité + profil (mirroring
/// <c>glpi_profiles_users</c>), avec l'option "récursif" (l'habilitation s'applique aussi aux
/// sous-entités de <see cref="Entity"/>). Un utilisateur peut avoir plusieurs habilitations —
/// contrairement à une première version simplifiée de GlpiNg qui n'autorisait qu'une seule
/// entité et un seul profil par utilisateur.
/// </summary>
public class GlpiUserProfile
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public GlpiUser User { get; set; } = null!;

    public int EntityId { get; set; }
    public GlpiEntity Entity { get; set; } = null!;

    public int ProfileId { get; set; }
    public GlpiProfile Profile { get; set; } = null!;

    public bool IsRecursive { get; set; }
}
