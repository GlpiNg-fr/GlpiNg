using GlpiNg.Modules.Abstractions.Entities;

namespace GlpiNg.Web.Models;

/// <summary>
/// Groupe GLPI (regroupement d'utilisateurs, hiérarchisable via <see cref="ParentId"/>).
/// Reprend les champs du formulaire "Groupe" de GLPI utiles hors module Assistance
/// (visibilité dans un ticket/projet non exploitée tant que le module Assistance
/// n'existe pas ici, mais conservée pour rester compatible avec un futur import GLPI).
/// </summary>
public class GlpiGroup : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }
    public string? Code { get; set; }

    /// <summary>Id du groupe GLPI d'origine (glpi_groups.id) — voir Import/GlpiAdminMySqlImportService, même principe que Computer.SourceGlpiId.</summary>
    public int? SourceGlpiId { get; set; }

    public int? ParentId { get; set; }
    public GlpiGroup? Parent { get; set; }
    public List<GlpiGroup> Children { get; set; } = [];

    public bool IsRecursive { get; set; }

    public bool VisibleAsRequester { get; set; } = true;
    public bool VisibleAsObserver { get; set; } = true;
    public bool VisibleAsAssignee { get; set; } = true;
    public bool VisibleAsTask { get; set; } = true;
    public bool CanBeNotified { get; set; } = true;

    public bool CanBeProjectSupervisor { get; set; } = true;

    public bool CanContainItems { get; set; }
    public bool CanContainUsers { get; set; } = true;

    // Onglet "Sécurité" de GLPI (juste un "Forcer 2FA" sur l'instance de référence utilisée ici,
    // comme pour GlpiEntity.TwoFactorAuthRequired).
    public bool TwoFactorAuthRequired { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<GlpiGroupUser> Members { get; set; } = [];
}
