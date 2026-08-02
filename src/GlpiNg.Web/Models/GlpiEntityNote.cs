namespace GlpiNg.Web.Models;

/// <summary>Note libre rattachée à une <see cref="GlpiEntity"/> (onglet "Notes" de GLPI).</summary>
public class GlpiEntityNote
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public GlpiEntity Entity { get; set; } = null!;

    public required string Content { get; set; }
    public required string Author { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
