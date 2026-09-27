namespace GlpiNg.Web.Models;

/// <summary>
/// Entité GLPI (unité organisationnelle hiérarchisable via <see cref="ParentId"/>, reprise du
/// formulaire "Entité" de GLPI). Les champs d'adresse correspondent au bloc "Adresse" de la
/// fiche GLPI ; les objets (utilisateurs, groupes, postes...) ne sont pas encore rattachés à une
/// entité tant que le rattachement n'est pas implémenté ailleurs.
/// </summary>
public class GlpiEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }

    /// <summary>Id de l'entité GLPI d'origine (glpi_entities.id) — voir Import/GlpiAdminMySqlImportService, même principe que Computer.SourceGlpiId.</summary>
    public int? SourceGlpiId { get; set; }

    public int? ParentId { get; set; }
    public GlpiEntity? Parent { get; set; }
    public List<GlpiEntity> Children { get; set; } = [];

    public string? Address { get; set; }
    public string? Postcode { get; set; }
    public string? Town { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Website { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Registration { get; set; }
    public string? Longitude { get; set; }
    public string? Latitude { get; set; }
    public string? Altitude { get; set; }

    // Onglet "Informations avancées" de GLPI : sur une vraie instance GLPI, ce sont des
    // critères d'auto-affectation d'un élément importé (LDAP, TAG, domaine mail) à une entité.
    // Seuls le TAG et le domaine mail sont repris (pas encore branchés sur l'import
    // d'inventaire ni sur AuthLdapServer, mais le TAG matche déjà ce que GlpiMySqlImportService
    // pourrait exploiter plus tard).
    public string? AssignmentTag { get; set; }
    public string? AssignmentEmailDomain { get; set; }

    // Onglet "Sécurité" de GLPI (juste un "Forcer 2FA" sur l'instance de référence utilisée ici).
    public bool TwoFactorAuthRequired { get; set; }

    // Onglet "Parc" de GLPI : le seul champ pertinent hors modules financiers/logiciels/transfert
    // (absents de GlpiNg) est l'URL de base utilisée par l'agent, directement liée au protocole
    // /inventory de ce projet.
    public string? AgentBaseUrl { get; set; }

    // Onglet "Personnalisation de l'interface" de GLPI (juste le CSS personnalisé sur l'instance
    // de référence utilisée ici).
    public bool CustomCssEnabled { get; set; }
    public string? CustomCss { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
