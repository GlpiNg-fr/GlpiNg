using System.Globalization;
using System.Security.Claims;
using GlpiNg.Modules.Abstractions.Reservations;
using GlpiNg.Modules.Reservations.Models;
using GlpiNg.Modules.Reservations.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Modules.Reservations.Components.Pages.Reservations;

/// <summary>
/// Planning des réservations : une grille mensuelle, et la liste de ce qui vient.
///
/// La grille est construite à la main plutôt qu'avec un composant de calendrier : elle n'affiche
/// que des créneaux déjà chargés, sur une seule page, et une dépendance de plus pour dessiner
/// trente-cinq cases ne se justifie pas.
/// </summary>
public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<DbContext> DbFactory { get; set; } = null!;

    [Inject]
    private ReservationService Reservations { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private List<ReservableItem> _reservableItems = [];
    private List<Reservation> _monthReservations = [];
    private List<Reservation> _upcoming = [];

    /// <summary>Premier jour du mois affiché.</summary>
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private int _newItemId;
    private DateTime _newBegin = DateTime.Today.AddHours(9);
    private DateTime _newEnd = DateTime.Today.AddHours(10);
    private string? _newComment;
    private ReservationRepeat _newRepeat = ReservationRepeat.None;
    private int _newOccurrences = 1;
    private string? _error;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using DbContext db = await DbFactory.CreateDbContextAsync();

        _reservableItems = await db.Set<ReservableItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.ItemName)
            .ToListAsync();

        if (_newItemId == 0 || _reservableItems.All(item => item.Id != _newItemId))
        {
            _newItemId = _reservableItems.FirstOrDefault()?.Id ?? 0;
        }

        // La grille montre les semaines complètes du mois : ses bornes débordent donc sur les mois
        // voisins, et les créneaux chargés doivent déborder avec elles.
        DateTime from = FirstVisibleDay;
        DateTime to = from.AddDays(42);

        _monthReservations = await db.Set<Reservation>()
            .AsNoTracking()
            .Include(reservation => reservation.ReservableItem)
            .Where(reservation => reservation.Begin < to && reservation.End > from)
            .OrderBy(reservation => reservation.Begin)
            .ToListAsync();

        DateTime now = DateTime.Now;

        _upcoming = await db.Set<Reservation>()
            .AsNoTracking()
            .Include(reservation => reservation.ReservableItem)
            .Where(reservation => reservation.End > now)
            .OrderBy(reservation => reservation.Begin)
            .Take(50)
            .ToListAsync();
    }

    /// <summary>
    /// Noms des jours : pris à la culture courante, qui suit la langue choisie par l'utilisateur
    /// (voir « Traductions » dans CLAUDE.md — les noms de jours ne passent pas par le catalogue).
    /// La semaine commence lundi, comme dans GLPI.
    /// </summary>
    private static IEnumerable<string> DayHeaders =>
        Enumerable.Range(0, 7).Select(offset =>
            Capitalize(CultureInfo.CurrentUICulture.DateTimeFormat.AbbreviatedDayNames[
                ((int)DayOfWeek.Monday + offset) % 7]));

    private string MonthLabel => _month.ToString("MMMM yyyy", CultureInfo.CurrentUICulture);

    /// <summary>Lundi de la semaine contenant le 1er du mois affiché.</summary>
    private DateTime FirstVisibleDay => _month.AddDays(-(((int)_month.DayOfWeek + 6) % 7));

    /// <summary>Six semaines, toujours : une grille dont la hauteur change à chaque mois saute à l'œil.</summary>
    private IEnumerable<DateTime> Weeks =>
        Enumerable.Range(0, 6).Select(week => FirstVisibleDay.AddDays(7 * week));

    private string? SelectedItemComment =>
        _reservableItems.FirstOrDefault(item => item.Id == _newItemId)?.Comment;

    private IEnumerable<Reservation> OnDay(DateTime day) =>
        _monthReservations
            .Where(reservation => reservation.Begin.Date <= day.Date && reservation.End.Date >= day.Date)
            .OrderBy(reservation => reservation.Begin);

    private string Tooltip(Reservation reservation) => string.Format(
        CultureInfo.CurrentUICulture,
        "{0} — {1} ({2})",
        reservation.ReservableItem?.ItemName,
        $"{reservation.Begin:dd/MM HH:mm} → {reservation.End:dd/MM HH:mm}",
        reservation.UserName);

    private async Task ShiftMonth(int months)
    {
        _month = _month.AddMonths(months);
        await LoadAsync();
    }

    private async Task GoToday()
    {
        _month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadAsync();
    }

    private async Task OpenNew(DateTime? day)
    {
        _error = null;

        if (day is { } chosen)
        {
            _newBegin = chosen.Date.AddHours(9);
            _newEnd = chosen.Date.AddHours(10);
        }

        await JS.InvokeVoidAsync("glping.showModal", "newReservationModal");
    }

    private async Task CreateAsync()
    {
        _error = null;

        if (_newItemId == 0)
        {
            return;
        }

        ReservationOutcome outcome = await Reservations.ReserveByItemIdAsync(
            _newItemId,
            await CurrentUserIdAsync(),
            await CurrentUserNameAsync(),
            _newBegin,
            _newEnd,
            _newComment,
            _newRepeat,
            _newRepeat == ReservationRepeat.None ? 1 : _newOccurrences);

        if (!outcome.Ok)
        {
            // Le refus reste dans la fenêtre : la fermer effacerait la saisie à corriger.
            _error = outcome.Error;
            return;
        }

        _newComment = null;
        _newRepeat = ReservationRepeat.None;
        _newOccurrences = 1;

        await JS.InvokeVoidAsync("glping.hideModal", "newReservationModal");
        await LoadAsync();
    }

    private async Task CancelAsync(int reservationId, bool wholeSeries)
    {
        await Reservations.CancelAsync(reservationId, wholeSeries);
        await LoadAsync();
    }

    private async Task<string> CurrentUserNameAsync()
    {
        if (AuthStateTask is null)
        {
            return "Système";
        }

        AuthenticationState authState = await AuthStateTask;
        return authState.User.Identity?.Name is { Length: > 0 } name ? name : "Système";
    }

    private async Task<int?> CurrentUserIdAsync()
    {
        if (AuthStateTask is null)
        {
            return null;
        }

        AuthenticationState authState = await AuthStateTask;

        return int.TryParse(authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id)
            ? id
            : null;
    }

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpper(value[0], CultureInfo.CurrentUICulture) + value[1..];
}
