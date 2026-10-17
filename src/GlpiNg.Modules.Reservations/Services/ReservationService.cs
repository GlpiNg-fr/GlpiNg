using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Abstractions.Localization;
using GlpiNg.Modules.Abstractions.Notifications;
using GlpiNg.Modules.Abstractions.Reservations;
using GlpiNg.Modules.Reservations.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Modules.Reservations.Services;

/// <summary>
/// Les règles des réservations : autorisation, pose d'un créneau, contrôle de chevauchement,
/// annulation. Rend <see cref="IItemReservations"/> aux autres modules et sert aussi les pages du
/// module, qui n'ont donc pas de seconde implémentation des mêmes règles.
///
/// Le contrôle de chevauchement est fait ici, à un seul endroit : une page qui réserverait en
/// écrivant directement en base pourrait poser deux créneaux sur le même objet à la même heure, et
/// c'est précisément ce qu'une réservation est censée empêcher.
/// </summary>
public sealed class ReservationService(
    IDbContextFactory<DbContext> dbFactory,
    INotificationPublisher? notifications = null) : IItemReservations
{
    /// <summary>Nombre maximal d'occurrences d'une série, garde-fou de saisie.</summary>
    public const int MaxOccurrences = 52;

    public async Task<bool> IsReservableAsync(string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Set<ReservableItem>()
            .AsNoTracking()
            .AnyAsync(item => item.ItemType == itemType && item.ItemId == itemId && item.IsActive, cancellationToken);
    }

    public async Task<int> CountForItemAsync(string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // « À venir » inclut le créneau en cours : un objet réservé jusqu'à ce soir n'est pas libre.
        DateTime now = DateTime.Now;

        return await db.Set<Reservation>()
            .AsNoTracking()
            .CountAsync(
                reservation => reservation.ReservableItem!.ItemType == itemType
                    && reservation.ReservableItem.ItemId == itemId
                    && reservation.End > now,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ItemReservation>> GetForItemAsync(string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<Reservation> reservations = await db.Set<Reservation>()
            .AsNoTracking()
            .Include(reservation => reservation.ReservableItem)
            .Where(reservation => reservation.ReservableItem!.ItemType == itemType
                && reservation.ReservableItem.ItemId == itemId)
            .OrderBy(reservation => reservation.Begin)
            .ToListAsync(cancellationToken);

        return [.. reservations.Select(Project)];
    }

    public async Task<IReadOnlyList<ItemReservation>> GetForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<Reservation> reservations = await db.Set<Reservation>()
            .AsNoTracking()
            .Include(reservation => reservation.ReservableItem)
            .Where(reservation => reservation.UserId == userId)
            .OrderBy(reservation => reservation.Begin)
            .ToListAsync(cancellationToken);

        return [.. reservations.Select(Project)];
    }

    public async Task EnableAsync(
        string itemType,
        int itemId,
        string itemName,
        string? itemUrl,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        ReservableItem? existing = await db.Set<ReservableItem>()
            .FirstOrDefaultAsync(item => item.ItemType == itemType && item.ItemId == itemId, cancellationToken);

        if (existing is null)
        {
            db.Set<ReservableItem>().Add(new ReservableItem
            {
                ItemType = itemType,
                ItemId = itemId,
                ItemName = itemName,
                ItemUrl = itemUrl,
                Comment = comment,
            });
        }
        else
        {
            // Réactivation : le nom et le lien sont rafraîchis au passage, c'est le seul moment où
            // le module les apprend (voir la doc de ReservableItem).
            existing.IsActive = true;
            existing.ItemName = itemName;
            existing.ItemUrl = itemUrl;

            if (comment is not null)
            {
                existing.Comment = comment;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableAsync(string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        ReservableItem? existing = await db.Set<ReservableItem>()
            .FirstOrDefaultAsync(item => item.ItemType == itemType && item.ItemId == itemId, cancellationToken);

        if (existing is null)
        {
            return;
        }

        db.Set<ReservableItem>().Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReservationOutcome> ReserveAsync(
        string itemType,
        int itemId,
        int? userId,
        string userName,
        DateTime begin,
        DateTime end,
        string? comment,
        ReservationRepeat repeat = ReservationRepeat.None,
        int occurrences = 1,
        CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        ReservableItem? item = await db.Set<ReservableItem>()
            .FirstOrDefaultAsync(candidate => candidate.ItemType == itemType && candidate.ItemId == itemId, cancellationToken);

        if (item is null || !item.IsActive)
        {
            return new ReservationOutcome(null, Tr.T("Cet élément n'est pas réservable."));
        }

        return await ReserveCoreAsync(db, item, userId, userName, begin, end, comment, repeat, occurrences, cancellationToken);
    }

    /// <summary>
    /// Même geste que <see cref="ReserveAsync"/>, mais depuis l'identifiant de l'autorisation :
    /// c'est ce que manipulent les pages du module, qui listent les éléments réservables et non
    /// les objets du parc.
    /// </summary>
    public async Task<ReservationOutcome> ReserveByItemIdAsync(
        int reservableItemId,
        int? userId,
        string userName,
        DateTime begin,
        DateTime end,
        string? comment,
        ReservationRepeat repeat = ReservationRepeat.None,
        int occurrences = 1,
        CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        ReservableItem? item = await db.Set<ReservableItem>()
            .FirstOrDefaultAsync(candidate => candidate.Id == reservableItemId, cancellationToken);

        if (item is null || !item.IsActive)
        {
            return new ReservationOutcome(null, Tr.T("Cet élément n'est pas réservable."));
        }

        return await ReserveCoreAsync(db, item, userId, userName, begin, end, comment, repeat, occurrences, cancellationToken);
    }

    private async Task<ReservationOutcome> ReserveCoreAsync(
        DbContext db,
        ReservableItem item,
        int? userId,
        string userName,
        DateTime begin,
        DateTime end,
        string? comment,
        ReservationRepeat repeat,
        int occurrences,
        CancellationToken cancellationToken)
    {
        if (end <= begin)
        {
            return new ReservationOutcome(null, Tr.T("La fin doit être postérieure au début."));
        }

        List<(DateTime Begin, DateTime End)> slots = Expand(begin, end, repeat, occurrences);

        if (slots.Count == 0)
        {
            return new ReservationOutcome(null, Tr.T("Nombre d'occurrences invalide."));
        }

        // Les créneaux déjà posés sur cet élément, chargés une fois : la comparaison porte sur des
        // heures murales, et la faire en SQL n'apporterait rien sur le volume d'un objet unique.
        DateTime earliest = slots[0].Begin;

        List<Reservation> taken = await db.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => reservation.ReservableItemId == item.Id && reservation.End > earliest)
            .ToListAsync(cancellationToken);

        foreach ((DateTime slotBegin, DateTime slotEnd) in slots)
        {
            Reservation? clash = taken.FirstOrDefault(
                reservation => reservation.Begin < slotEnd && reservation.End > slotBegin);

            if (clash is not null)
            {
                // Message volontairement précis : « déjà réservé » sans dire par qui ni quand
                // oblige à aller chercher le planning pour comprendre le refus.
                return new ReservationOutcome(null, Tr.T(
                    "Créneau déjà réservé par {0}, du {1} au {2}.",
                    clash.UserName,
                    clash.Begin.ToString("dd/MM/yyyy HH:mm"),
                    clash.End.ToString("dd/MM/yyyy HH:mm")));
            }
        }

        Guid? seriesId = slots.Count > 1 ? Guid.NewGuid() : null;

        List<Reservation> created = [.. slots.Select(slot => new Reservation
        {
            ReservableItemId = item.Id,
            UserId = userId,
            UserName = userName,
            Begin = slot.Begin,
            End = slot.End,
            Comment = comment,
            SeriesId = seriesId,
        })];

        db.Set<Reservation>().AddRange(created);
        await db.SaveChangesAsync(cancellationToken);

        await PublishAsync("new", created[0], item, cancellationToken);

        return new ReservationOutcome(created[0].Id, null);
    }

    public async Task CancelAsync(int reservationId, bool wholeSeries, CancellationToken cancellationToken = default)
    {
        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        Reservation? reservation = await db.Set<Reservation>()
            .Include(candidate => candidate.ReservableItem)
            .FirstOrDefaultAsync(candidate => candidate.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return;
        }

        List<Reservation> toRemove = [reservation];

        if (wholeSeries && reservation.SeriesId is { } seriesId)
        {
            toRemove = await db.Set<Reservation>()
                .Where(candidate => candidate.SeriesId == seriesId)
                .ToListAsync(cancellationToken);
        }

        db.Set<Reservation>().RemoveRange(toRemove);
        await db.SaveChangesAsync(cancellationToken);

        if (reservation.ReservableItem is { } item)
        {
            await PublishAsync("delete", reservation, item, cancellationToken);
        }
    }

    /// <summary>
    /// Déploie une série en créneaux concrets. Le pas s'applique au début comme à la fin, donc une
    /// réservation à cheval sur deux jours reste à cheval à chaque occurrence.
    /// </summary>
    private static List<(DateTime Begin, DateTime End)> Expand(
        DateTime begin,
        DateTime end,
        ReservationRepeat repeat,
        int occurrences)
    {
        if (repeat == ReservationRepeat.None)
        {
            occurrences = 1;
        }

        if (occurrences < 1 || occurrences > MaxOccurrences)
        {
            return [];
        }

        return
        [
            .. Enumerable.Range(0, occurrences)
                .Select(step => (Shift(begin, repeat, step), Shift(end, repeat, step))),
        ];
    }

    private static DateTime Shift(DateTime value, ReservationRepeat repeat, int step) => repeat switch
    {
        ReservationRepeat.Daily => value.AddDays(step),
        ReservationRepeat.Weekly => value.AddDays(7 * step),
        // AddMonths cale sur le dernier jour du mois quand le jour n'existe pas (31 → 30) : c'est
        // le comportement attendu d'un « chaque mois », et il évite de sauter une occurrence.
        ReservationRepeat.Monthly => value.AddMonths(step),
        _ => value,
    };

    /// <summary>
    /// Publie l'événement au catalogue de l'hôte (type « Reservation »). Le publieur est facultatif
    /// — sans module de notifications, une réservation se pose quand même.
    /// </summary>
    private async Task PublishAsync(
        string eventKey,
        Reservation reservation,
        ReservableItem item,
        CancellationToken cancellationToken)
    {
        if (notifications is null)
        {
            return;
        }

        await notifications.PublishAsync(
            ItemTypes.Reservation,
            eventKey,
            reservation.Id,
            new Dictionary<string, string?>
            {
                // Format fixe, comme tout ce qui est écrit une fois et relu par tous : ces valeurs
                // partent dans un courriel, pas dans un rendu dépendant du lecteur.
                ["reservation.item"] = item.ItemName,
                ["reservation.itemtype"] = item.ItemType,
                ["reservation.user"] = reservation.UserName,
                ["reservation.begin"] = reservation.Begin.ToString("dd/MM/yyyy HH:mm"),
                ["reservation.end"] = reservation.End.ToString("dd/MM/yyyy HH:mm"),
                ["reservation.comment"] = reservation.Comment,
                ["reservation.url"] = "/tools/reservations",
            },
            cancellationToken);
    }

    private static ItemReservation Project(Reservation reservation) => new(
        reservation.Id,
        reservation.ReservableItem?.ItemType ?? string.Empty,
        reservation.ReservableItem?.ItemId ?? 0,
        reservation.ReservableItem?.ItemName ?? string.Empty,
        reservation.ReservableItem?.ItemUrl,
        reservation.UserId,
        reservation.UserName,
        reservation.Begin,
        reservation.End,
        reservation.Comment,
        reservation.SeriesId is not null);
}
