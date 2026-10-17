using GlpiNg.Modules.Reservations.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Modules.Reservations.Components.Pages.Reservations;

/// <summary>
/// Liste des autorisations de réservation (équivalent de « Administration » &gt; éléments
/// réservables de GLPI) : suspendre, annoter, retirer.
///
/// Rien ne s'ajoute d'ici, volontairement : l'autorisation naît sur la fiche de l'objet, seule à
/// connaître son nom et son lien (voir la doc de <see cref="ReservableItem"/>). Un sélecteur
/// d'objet ici supposerait que ce module sache lire les modèles du parc, ce qu'il ne peut pas.
/// </summary>
public partial class Items : ComponentBase
{
    [Inject]
    private IDbContextFactory<DbContext> DbFactory { get; set; } = null!;

    private List<ReservableItem> _items = [];
    private Dictionary<int, int> _counts = [];
    private string _searchTerm = string.Empty;

    private List<ReservableItem> Filtered
    {
        get
        {
            string term = _searchTerm.Trim();

            return term.Length == 0
                ? _items
                : [.. _items.Where(item =>
                    item.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || item.ItemType.Contains(term, StringComparison.OrdinalIgnoreCase))];
        }
    }

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using DbContext db = await DbFactory.CreateDbContextAsync();

        _items = await db.Set<ReservableItem>()
            .AsNoTracking()
            .OrderBy(item => item.ItemName)
            .ToListAsync();

        _counts = await db.Set<Reservation>()
            .AsNoTracking()
            .GroupBy(reservation => reservation.ReservableItemId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Count);
    }

    private async Task ToggleAsync(ReservableItem item, bool active)
    {
        await using DbContext db = await DbFactory.CreateDbContextAsync();

        await db.Set<ReservableItem>()
            .Where(candidate => candidate.Id == item.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.IsActive, active));

        item.IsActive = active;
    }

    private async Task SaveCommentAsync(ReservableItem item, string? comment)
    {
        await using DbContext db = await DbFactory.CreateDbContextAsync();

        await db.Set<ReservableItem>()
            .Where(candidate => candidate.Id == item.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(candidate => candidate.Comment, comment));

        item.Comment = comment;
    }

    private async Task RemoveAsync(ReservableItem item)
    {
        await using DbContext db = await DbFactory.CreateDbContextAsync();

        // Les réservations partent en cascade (voir ConfigureReservations dans GlpiNgDbContext) :
        // elles n'ont pas de sens sans l'autorisation qui les portait.
        await db.Set<ReservableItem>()
            .Where(candidate => candidate.Id == item.Id)
            .ExecuteDeleteAsync();

        await LoadAsync();
    }
}
