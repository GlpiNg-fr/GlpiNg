using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Abstractions.Notifications;
using GlpiNg.Modules.Reservations.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Modules.Reservations.Services;

/// <summary>
/// Rappel d'échéance des réservations, équivalent de l'action automatique « reservation » de GLPI :
/// publie un événement <c>alert</c> pour chaque créneau qui commence dans les prochaines
/// <see cref="LeadTimeHours"/> heures et dont le rappel n'est pas déjà parti.
///
/// <c>Reservation.AlertSentAt</c> est le garde-fou : le service cron repasse à chaque tick, et sans
/// marquage la même réservation serait rappelée à chaque passage jusqu'à son début.
/// </summary>
public sealed class ReservationAlertCronTask(
    IDbContextFactory<DbContext> dbFactory,
    INotificationPublisher notifications) : ICronTask
{
    /// <summary>Avance du rappel, en heures. Pas réglable : une réservation se prépare la veille.</summary>
    public const int LeadTimeHours = 24;

    public string Key => "reservation_alert";

    public string Name => "Rappel des réservations à venir";

    public string Description =>
        "Publie l'événement « Réservation : rappel d'échéance » pour les créneaux qui commencent "
        + $"dans moins de {LeadTimeHours} heures, une seule fois par réservation.";

    public int DefaultFrequencyMinutes => 60;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Begin est une heure murale (voir Reservation) : la comparaison se fait donc sur l'heure
        // locale du serveur, pas sur UtcNow — c'est le seul repère commun avec la saisie.
        DateTime now = DateTime.Now;
        DateTime horizon = now.AddHours(LeadTimeHours);

        List<Reservation> due = await db.Set<Reservation>()
            .Include(reservation => reservation.ReservableItem)
            .Where(reservation => reservation.AlertSentAt == null
                && reservation.Begin > now
                && reservation.Begin <= horizon)
            .ToListAsync(cancellationToken);

        foreach (Reservation reservation in due)
        {
            await notifications.PublishAsync(
                ItemTypes.Reservation,
                "alert",
                reservation.Id,
                new Dictionary<string, string?>
                {
                    ["reservation.item"] = reservation.ReservableItem?.ItemName,
                    ["reservation.itemtype"] = reservation.ReservableItem?.ItemType,
                    ["reservation.user"] = reservation.UserName,
                    ["reservation.begin"] = reservation.Begin.ToString("dd/MM/yyyy HH:mm"),
                    ["reservation.end"] = reservation.End.ToString("dd/MM/yyyy HH:mm"),
                    ["reservation.comment"] = reservation.Comment,
                    ["reservation.url"] = "/tools/reservations",
                },
                cancellationToken);

            reservation.AlertSentAt = DateTime.UtcNow;
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
