using GlpiNg.Modules.Abstractions.Entities;

namespace GlpiNg.Modules.Reservations.Models;

/// <summary>
/// Autorisation de réserver un objet, équivalent de <c>glpi_reservationitems</c> : dans GLPI comme
/// ici, un objet du parc n'est pas réservable par défaut — quelqu'un le déclare tel depuis sa
/// fiche, et c'est cette ligne-là que les réservations visent.
///
/// L'objet est désigné par un couple <see cref="ItemType"/>/<see cref="ItemId"/> plutôt que par une
/// clé étrangère : le module ne connaît aucun modèle du parc (voir son .csproj), et c'est ce qui
/// permet de réserver un ordinateur, un moniteur ou un actif personnalisé sans le savoir.
///
/// <see cref="ItemName"/> et <see cref="ItemUrl"/> sont <b>dénormalisés</b>, pour la même raison :
/// personne ici ne peut faire la jointure qui les retrouverait. La fiche qui déclare l'objet
/// réservable les fournit et les rafraîchit à chaque visite — un objet renommé garde donc son
/// ancien nom dans la liste jusque-là, ce qui est préférable à une liste de « #42 ».
/// </summary>
public class ReservableItem : IEntityScoped
{
    /// <inheritdoc />
    public int? EntityId { get; set; }

    /// <inheritdoc />
    public bool IsRecursive { get; set; }

    public int Id { get; set; }

    /// <summary>Type de l'objet réservable, au sens d'<c>Abstractions/Items/ItemTypes</c>.</summary>
    public required string ItemType { get; set; }

    /// <summary>Identifiant de l'objet réservable dans la table de son propre type.</summary>
    public int ItemId { get; set; }

    /// <summary>Nom de l'objet au moment de la dernière visite de sa fiche — voir la doc de la classe.</summary>
    public required string ItemName { get; set; }

    /// <summary>Lien vers la fiche de l'objet, fourni par celle-ci — voir la doc de la classe.</summary>
    public string? ItemUrl { get; set; }

    /// <summary>
    /// Réservation suspendue : l'objet reste dans la liste avec son historique, mais plus aucun
    /// créneau ne peut être posé. C'est ce qu'on veut d'un vidéoprojecteur en réparation —
    /// retirer l'autorisation effacerait les réservations déjà prises.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Consigne affichée à celui qui réserve (« à rendre au secrétariat »...).</summary>
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Reservation> Reservations { get; set; } = [];
}
