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
/// gestion, outils, administration, configuration). Ces droits ne sont pour l'instant
/// pas branchés sur une vérification d'autorisation réelle : GlpiNg n'a aujourd'hui aucune
/// application de permissions par page (seul <see cref="GlpiUser.IsAdmin"/> existe, et il n'est
/// lui-même vérifié nulle part) — les stocker ici prépare le terrain sans prétendre les appliquer.
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
