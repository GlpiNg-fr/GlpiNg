namespace GlpiNg.Web.Models;

/// <summary>Appartenance d'un <see cref="GlpiUser"/> à un <see cref="GlpiGroup"/> (clé composite GroupId/UserId).</summary>
public class GlpiGroupUser
{
    public int GroupId { get; set; }
    public GlpiGroup Group { get; set; } = null!;

    public int UserId { get; set; }
    public GlpiUser User { get; set; } = null!;
}
