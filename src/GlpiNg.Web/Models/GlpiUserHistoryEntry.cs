namespace GlpiNg.Web.Models;

/// <summary>
/// Entrée d'historique d'un <see cref="GlpiUser"/> (onglet "Historique" de GLPI), reprend la
/// forme de <see cref="GlpiEntityHistoryEntry"/> / <see cref="GlpiGroupHistoryEntry"/>.
/// </summary>
public class GlpiUserHistoryEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public required string User { get; set; }
    public required string Field { get; set; }
    public required string Description { get; set; }
}
