using GlpiNg.Modules.Abstractions.Items;

namespace GlpiNg.Web.Models.Notes;

/// <summary>
/// Une note libre rattachée à un objet, équivalent de <c>glpi_notepads</c> : l'onglet « Notes »
/// que GLPI pose sur presque toutes ses fiches.
///
/// Référence polymorphe non contrainte (<see cref="ItemType"/> + <see cref="ItemId"/>, sans clé
/// étrangère), comme GLPI et comme <see cref="Documents.DocumentItem"/> : les objets visés vivent
/// dans des modules que l'hôte ne peut pas tous connaître, et une table par type — la voie qu'ont
/// prise <c>GlpiEntityNote</c> et <c>GlpiGroupNote</c> — oblige à tout réécrire à chaque fiche
/// nouvelle.
///
/// <b>Pas de cloisonnement par entité sur la note elle-même.</b> Elle n'implémente pas
/// <c>IEntityScoped</c>, et c'est délibéré : la visibilité d'une note suit l'objet qui la porte,
/// comme celle d'une pièce jointe. Lui donner sa propre entité reproduirait exactement le défaut
/// qui a fait disparaître les documents des articles — un objet visible dont les annexes ne le
/// sont pas, sans le moindre message.
///
/// Plusieurs notes par objet, et non une seule zone de texte : c'est ce que fait GLPI depuis 9.2,
/// et c'est ce qui permet de savoir qui a écrit quoi et quand.
/// </summary>
public class Notepad
{
    public int Id { get; set; }

    /// <summary>Type de l'objet porteur — voir <see cref="ItemTypes"/>.</summary>
    public required string ItemType { get; set; }

    public int ItemId { get; set; }

    public required string Content { get; set; }

    /// <summary>Compte ayant écrit la note, s'il existe encore ; le nom est conservé à part.</summary>
    public int? AuthorUserId { get; set; }

    /// <summary>
    /// Nom de l'auteur au moment de la rédaction. Dupliqué plutôt que résolu à l'affichage : une
    /// note survit au compte qui l'a écrite, et « note de (compte supprimé) » est une perte
    /// d'information gratuite.
    /// </summary>
    public required string AuthorName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Nom du dernier compte ayant modifié la note — même raison que <see cref="AuthorName"/>.</summary>
    public string? LastEditorName { get; set; }

    /// <summary>Identifiant GLPI d'origine (<c>glpi_notepads.id</c>), quand la note vient de l'import.</summary>
    public int? SourceGlpiId { get; set; }
}
