using GlpiNg.Modules.Abstractions.Entities;

namespace GlpiNg.Web.Models.Notifications;

/// <summary>
/// Gabarit d'e-mail (glpi_notificationtemplates + sa traduction par défaut côté GLPI, fusionnés
/// ici en une seule entité : GlpiNg n'a pas de préférence de langue par utilisateur à ce jour,
/// donc la traduction par langue de GLPI n'apporterait rien de plus qu'une couche de sélection
/// inutilisable). <see cref="Subject"/>/<see cref="ContentText"/>/<see cref="ContentHtml"/>
/// acceptent des balises ##cle## substituées par <see cref="Services.Notifications.NotificationDispatchService"/>
/// au moment de l'envoi — voir <see cref="NotificationEventCatalog"/> pour la liste des balises
/// disponibles selon <see cref="ItemType"/>.
/// </summary>
public class NotificationTemplate : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    /// <inheritdoc />
    public bool IsRecursive { get; set; }

    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }

    /// <summary>Clé du type d'objet (voir <see cref="NotificationEventCatalog"/>) déterminant les balises ##...## disponibles.</summary>
    public required string ItemType { get; set; }

    public required string Subject { get; set; }
    public string? ContentText { get; set; }
    public string? ContentHtml { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
