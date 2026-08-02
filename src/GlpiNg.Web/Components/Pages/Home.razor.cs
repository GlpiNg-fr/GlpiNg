using System.Security.Claims;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private sealed record DashboardCardModel(string Key, string Title, string Icon, string BadgeClass, int Value, string Href);

    private List<DashboardCardModel> _allCards = [];
    private List<DashboardCardModel> _cards = [];
    private int? _userId;
    private int? _draggedIndex;
    private bool _isEditMode;
    private string? _selectedCardKeyToAdd;

    private List<DashboardCardModel> AvailableCards =>
        _allCards.Where(c => _cards.All(v => v.Key != c.Key)).ToList();

    protected override async Task OnInitializedAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        var computerCount = await db.Computers.CountAsync();
        var agentCount = await db.Agents.CountAsync();

        _allCards =
        [
            new("computers", "Postes inventoriés", "ti-device-desktop", "bg-primary", computerCount, "/computers"),
            new("agents", "Agents enregistrés", "ti-cpu", "bg-azure", agentCount, "/agents"),
        ];

        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            string? userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _userId = int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        _cards = ApplySavedOrder(_allCards, await LoadSavedOrderAsync(db));
    }

    private async Task<string[]?> LoadSavedOrderAsync(GlpiNgDbContext db)
    {
        if (_userId is not int userId)
        {
            return null;
        }

        var pref = await db.DashboardCardPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        return pref?.CardOrder.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static List<DashboardCardModel> ApplySavedOrder(List<DashboardCardModel> allCards, string[]? savedOrder)
    {
        if (savedOrder is null)
        {
            return [.. allCards];
        }

        var byKey = allCards.ToDictionary(c => c.Key);
        return savedOrder.Where(byKey.ContainsKey).Select(k => byKey[k]).ToList();
    }

    private void ToggleEditMode()
    {
        _isEditMode = !_isEditMode;
        _selectedCardKeyToAdd = null;
    }

    private async Task AddSelectedCard()
    {
        if (string.IsNullOrEmpty(_selectedCardKeyToAdd))
        {
            return;
        }

        var card = _allCards.FirstOrDefault(c => c.Key == _selectedCardKeyToAdd);
        if (card is null)
        {
            return;
        }

        _cards.Add(card);
        _selectedCardKeyToAdd = null;
        await SaveOrderAsync();
    }

    private async Task RemoveCard(int index)
    {
        _cards.RemoveAt(index);
        await SaveOrderAsync();
    }

    private void OnCardDragStart(int index)
    {
        if (_isEditMode)
        {
            _draggedIndex = index;
        }
    }

    private void OnCardDragEnd() => _draggedIndex = null;

    private async Task OnCardDrop(int targetIndex)
    {
        if (!_isEditMode || _draggedIndex is not int sourceIndex || sourceIndex == targetIndex)
        {
            _draggedIndex = null;
            return;
        }

        var moved = _cards[sourceIndex];
        _cards.RemoveAt(sourceIndex);
        _cards.Insert(targetIndex, moved);
        _draggedIndex = null;

        await SaveOrderAsync();
    }

    private async Task SaveOrderAsync()
    {
        if (_userId is not int userId)
        {
            return;
        }

        var order = string.Join(',', _cards.Select(c => c.Key));

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        var pref = await db.DashboardCardPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

        if (pref is null)
        {
            db.DashboardCardPreferences.Add(new DashboardCardPreference { UserId = userId, CardOrder = order });
        }
        else
        {
            pref.CardOrder = order;
        }

        await db.SaveChangesAsync();
    }
}
