namespace GlpiNg.Web.Models;

/// <summary>
/// Entrée d'historique d'un <see cref="GlpiProfile"/> (onglet "Historique"), reprend la forme de
/// <see cref="GlpiEntityHistoryEntry"/> / <see cref="GlpiGroupHistoryEntry"/> / <see cref="GlpiUserHistoryEntry"/>.
/// </summary>
public class GlpiProfileHistoryEntry
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public required string User { get; set; }
    public required string Field { get; set; }
    public required string Description { get; set; }
}
