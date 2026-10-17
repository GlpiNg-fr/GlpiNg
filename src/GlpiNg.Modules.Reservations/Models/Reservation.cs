namespace GlpiNg.Modules.Reservations.Models;

/// <summary>
/// Un créneau posé sur un objet réservable, équivalent de <c>glpi_reservations</c>.
///
/// <see cref="Begin"/> et <see cref="End"/> sont des <b>heures locales murales</b>, stockées
/// telles qu'elles ont été saisies dans un champ <c>datetime-local</c> : « de 14 h à 16 h » reste
/// « de 14 h à 16 h », quel que soit le fuseau de celui qui regarde ensuite le planning. Elles ne
/// doivent donc jamais être converties — voir « Affichage des dates » dans CLAUDE.md, et
/// <c>Display.LocalDateTime</c> qui est le seul formatage correct ici.
/// </summary>
public class Reservation
{
    public int Id { get; set; }

    public int ReservableItemId { get; set; }
    public ReservableItem? ReservableItem { get; set; }

    /// <summary>Compte au nom duquel le créneau est posé, s'il existe encore.</summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Nom de celui qui a réservé, conservé à part : un planning se relit après le départ d'un
    /// agent, et « réservé par (compte supprimé) » est une perte d'information gratuite.
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>Début du créneau — heure locale murale, voir la doc de la classe.</summary>
    public DateTime Begin { get; set; }

    /// <summary>Fin du créneau — heure locale murale, voir la doc de la classe.</summary>
    public DateTime End { get; set; }

    public string? Comment { get; set; }

    /// <summary>
    /// Série de récurrence : partagé par toutes les occurrences créées d'un seul geste, ce qui
    /// permet de les annuler ensemble. <c>null</c> pour une réservation unique.
    /// </summary>
    public Guid? SeriesId { get; set; }

    /// <summary>
    /// Instant (UTC) auquel le rappel d'échéance a été publié, pour ne le publier qu'une fois —
    /// voir <c>ReservationAlertCronTask</c>. Cette date-ci est un instant réel, pas une heure
    /// murale : elle est écrite par le serveur, pas saisie.
    /// </summary>
    public DateTime? AlertSentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
