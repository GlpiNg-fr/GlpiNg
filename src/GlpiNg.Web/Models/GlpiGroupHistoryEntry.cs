namespace GlpiNg.Web.Models;

/// <summary>
/// Entrée d'historique d'un <see cref="GlpiGroup"/> (onglet "Historique" de GLPI), reprend la
/// forme de <see cref="GlpiEntityHistoryEntry"/> / <see cref="GlpiNg.Modules.Inventory.Models.ComputerHistoryEntry"/>.
/// </summary>
public class GlpiGroupHistoryEntry
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public required string User { get; set; }
    public required string Field { get; set; }
    public required string Description { get; set; }
}
