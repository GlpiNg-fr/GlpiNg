using System.Globalization;
using GlpiNg.Modules.Abstractions.Reports;
using GlpiNg.Modules.Reservations.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Modules.Reservations.Reports;

/// <summary>
/// Rapport du module, affiché avec les autres sur <c>/tools/reports</c> — voir
/// <see cref="IReportProvider"/>.
///
/// Il répond à la question qu'on se pose dès qu'un parc est partagé : ce qu'on a rendu réservable
/// sert-il ? Un vidéoprojecteur jamais réservé n'est pas forcément inutile — il est peut-être
/// introuvable, ou personne ne sait qu'il existe —, et c'est ce que le second tableau montre.
/// </summary>
public sealed class ReservationReportProvider(IDbContextFactory<DbContext> dbFactory) : IReportProvider
{
    internal const string OverviewKey = "reservations-overview";

    /// <summary>Écriture des durées : l'UI est en français en dur (voir le README).</summary>
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly ReportDefinition[] Definitions =
    [
        new(OverviewKey, "Réservations",
            "Taux d'utilisation des éléments réservables, et ceux que personne ne réserve.",
            "ti-calendar-event", "Réservations"),
    ];

    public IReadOnlyList<ReportDefinition> GetReports() => Definitions;

    public Task<IReadOnlyList<ReportFilter>> GetFiltersAsync(string reportKey, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ReportFilter>>(reportKey == OverviewKey
            ? [ReportFilter.Select("window", "Période",
                [
                    new("30", "30 derniers jours"),
                    new("90", "90 derniers jours"),
                    new("365", "12 derniers mois"),
                    new("all", "Depuis le début"),
                ], "90")]
            : []);

    public async Task<ReportResult?> RunAsync(string reportKey, ReportParameters parameters, CancellationToken cancellationToken = default)
    {
        if (reportKey != OverviewKey)
        {
            return null;
        }

        // Les créneaux sont des heures murales (voir Reservation) : la borne de période se calcule
        // donc sur l'heure locale, le même repère que celui de la saisie.
        DateTime? since = parameters.GetString("window") switch
        {
            "30" => DateTime.Now.AddDays(-30),
            "365" => DateTime.Now.AddDays(-365),
            "all" => null,
            _ => DateTime.Now.AddDays(-90),
        };

        await using DbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<ReservableItem> items = await db.Set<ReservableItem>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<Reservation> reservations = await db.Set<Reservation>()
            .AsNoTracking()
            .Where(reservation => since == null || reservation.Begin >= since)
            .ToListAsync(cancellationToken);

        int total = reservations.Count;

        List<ReportRow> byItem = [.. items
            .Select(item => new
            {
                item.ItemName,
                item.ItemType,
                item.ItemUrl,
                item.IsActive,
                Slots = reservations.Where(reservation => reservation.ReservableItemId == item.Id).ToList(),
            })
            .OrderByDescending(entry => entry.Slots.Count)
            .ThenBy(entry => entry.ItemName, StringComparer.CurrentCultureIgnoreCase)
            .Select(entry => new ReportRow(
            [
                entry.ItemUrl is { Length: > 0 } url
                    ? ReportCell.Link(entry.ItemName, url)
                    : ReportCell.Of(entry.ItemName),
                ReportCell.Of(entry.ItemType),
                ReportCell.Of(entry.IsActive ? "Oui" : "Suspendue"),
                ReportCell.Of(entry.Slots.Count),
                ReportCell.Of(Hours(entry.Slots)),
                ReportCell.Percent(entry.Slots.Count, total),
            ]))];

        if (byItem.Count > 0)
        {
            byItem.Add(new ReportRow(
            [
                ReportCell.Of("Total"),
                ReportCell.Of("—"),
                ReportCell.Of("—"),
                ReportCell.Of(total),
                ReportCell.Of(Hours(reservations)),
                ReportCell.Percent(total, total),
            ], IsTotal: true));
        }

        List<ReportRow> unused = [.. items
            .Where(item => reservations.All(reservation => reservation.ReservableItemId != item.Id))
            .OrderBy(item => item.ItemName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new ReportRow(
            [
                item.ItemUrl is { Length: > 0 } url
                    ? ReportCell.Link(item.ItemName, url)
                    : ReportCell.Of(item.ItemName),
                ReportCell.Of(item.ItemType),
                ReportCell.Of(item.Comment),
                ReportCell.Date(item.CreatedAt),
            ]))];

        return ReportResult.Of(
            new ReportTable("Réservations par élément",
            [
                new("Élément"),
                new("Type"),
                new("Réservable"),
                new("Créneaux", ReportColumnKind.Number),
                new("Heures réservées", ReportColumnKind.Number),
                new("Part des créneaux", ReportColumnKind.Share),
            ], byItem,
                "Aucun élément réservable : autorisez la réservation depuis la fiche d'un objet du parc."),
            new ReportTable("Éléments réservables jamais réservés sur la période",
            [
                new("Élément"),
                new("Type"),
                new("Consigne"),
                new("Réservable depuis le"),
            ], unused,
                "Tous les éléments réservables ont été réservés au moins une fois sur la période."));
    }

    private static string Hours(IReadOnlyCollection<Reservation> reservations)
        => reservations.Count == 0
            ? "0"
            : reservations.Sum(reservation => (reservation.End - reservation.Begin).TotalHours).ToString("N1", Fr);
}
