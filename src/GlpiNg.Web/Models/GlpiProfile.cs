namespace GlpiNg.Web.Models;

/// <summary>Niveau d'accès d'un <see cref="GlpiProfile"/> sur une section du menu GlpiNg.</summary>
public enum ProfileRightLevel
{
    None = 0,
    Read = 1,
    Write = 2,
}

/// <summary>
/// Profil GLPI (rôle nommé), simplifié par rapport à GLPI : au lieu de la matrice de droits très
/// fine par module (Ticket, Ordinateur, Règle...) sur des dizaines d'onglets, un seul droit par
/// grande section du menu GlpiNg (voir MainLayout.GroupOrder côté Web : parc, assistance,
/// gestion, outils, administration, configuration).
///
/// Ces droits sont appliqués à l'exécution : filtrage du menu et garde-fou de navigation dans
/// MainLayout, refus d'accès direct par URL dans SectionAccessMiddleware, et refus d'écriture dans
/// GlpiNgDbContext.EnforceWriteRights. Ils sont résolus pour l'entité active — une habilitation
/// associe un profil à une entité — puis portés par le cookie d'authentification (voir
/// ProfileRightsService). Un compte <see cref="GlpiUser.IsAdmin"/> n'est soumis à aucun de ces
/// contrôles.
/// </summary>
public class GlpiProfile
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }
    public bool IsDefault { get; set; }

    /// <summary>Id du profil GLPI d'origine (glpi_profiles.id) — voir Import/GlpiAdminMySqlImportService, même principe que Computer.SourceGlpiId.</summary>
    public int? SourceGlpiId { get; set; }

    public ProfileRightLevel ParcRight { get; set; }
    public ProfileRightLevel AssistanceRight { get; set; }
    public ProfileRightLevel GestionRight { get; set; }
    public ProfileRightLevel OutilsRight { get; set; }
    public ProfileRightLevel AdministrationRight { get; set; }
    public ProfileRightLevel ConfigurationRight { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
