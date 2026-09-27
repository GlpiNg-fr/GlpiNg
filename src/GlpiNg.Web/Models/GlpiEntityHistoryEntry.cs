namespace GlpiNg.Web.Models;

/// <summary>
/// Entrée d'historique d'une <see cref="GlpiEntity"/> (onglet "Historique" de GLPI), reprend la
/// forme de <see cref="GlpiNg.Modules.Inventory.Models.ComputerHistoryEntry"/>.
/// </summary>
public class GlpiEntityHistoryEntry
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public required string User { get; set; }
    public required string Field { get; set; }
    public required string Description { get; set; }
}
