namespace GlpiNg.Web.Models;

/// <summary>Note libre rattachée à un <see cref="GlpiGroup"/> (onglet "Notes" de GLPI).</summary>
public class GlpiGroupNote
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public GlpiGroup Group { get; set; } = null!;

    public required string Content { get; set; }
    public required string Author { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
