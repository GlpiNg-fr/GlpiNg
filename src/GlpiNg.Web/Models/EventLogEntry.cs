namespace GlpiNg.Web.Models;

/// <summary>Niveau de sévérité d'une <see cref="EventLogEntry"/>, affiché dans la colonne "Niveau" de /admin/logs.</summary>
public enum EventLogLevel
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3,
}

/// <summary>
/// Entrée du journal des évènements système ("Journaux" de /admin/logs — voir Program.cs
/// MainLayout, groupe "administration"), équivalent réduit de glpi_events / front/event.php
/// côté GLPI : évènements transverses (connexions, contacts d'agent GLPI-Agent, ...), pas liés
/// à l'édition d'un item précis — à distinguer des tables *HistoryEntry (ex.
/// <see cref="GlpiUserHistoryEntry"/>, équivalent glpi_logs) qui tracent les changements de
/// champ d'un item donné, et de ConfigHistoryService (changements de /config, journal fichier
/// séparé). ItemType/ItemId ne sont pas contraints par une FK EF Core : référence polymorphe
/// facultative (même principe que DeploymentPackageTarget.ItemId, voir GlpiNgDbContext).
/// </summary>
public class EventLogEntry
{
    public int Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public required string Service { get; set; }
    public EventLogLevel Level { get; set; }
    public required string Message { get; set; }
    public string? ItemType { get; set; }
    public int? ItemId { get; set; }
    public string? ItemLabel { get; set; }
}
