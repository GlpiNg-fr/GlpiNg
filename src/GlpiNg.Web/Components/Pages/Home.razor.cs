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
    private GlpiNgDbContext Db { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private sealed record DashboardCardModel(string Key, string Title, string Icon, string BadgeClass, int Value);

    private List<DashboardCardModel> _cards = [];
    private int? _userId;
    private int? _draggedIndex;

    protected override async Task OnInitializedAsync()
    {
        var computerCount = await Db.Computers.CountAsync();
        var agentCount = await Db.Agents.CountAsync();

        var defaultCards = new List<DashboardCardModel>
        {
            new("computers", "Postes inventoriés", "ti-device-desktop", "bg-primary", computerCount),
            new("agents", "Agents enregistrés", "ti-cpu", "bg-azure", agentCount),
        };

        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            string? userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _userId = int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        _cards = ApplySavedOrder(defaultCards, await LoadSavedOrderAsync());
    }

    private async Task<string[]> LoadSavedOrderAsync()
    {
        if (_userId is not int userId)
        {
            return [];
        }

        var pref = await Db.DashboardCardPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        return pref is null || string.IsNullOrWhiteSpace(pref.CardOrder)
            ? []
            : pref.CardOrder.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static List<DashboardCardModel> ApplySavedOrder(List<DashboardCardModel> cards, string[] savedOrder)
    {
        if (savedOrder.Length == 0)
        {
            return cards;
        }

        var byKey = cards.ToDictionary(c => c.Key);
        var ordered = savedOrder.Where(byKey.ContainsKey).Select(k => byKey[k]).ToList();
        ordered.AddRange(cards.Where(c => !savedOrder.Contains(c.Key)));
        return ordered;
    }

    private void OnCardDragStart(int index) => _draggedIndex = index;

    private void OnCardDragEnd() => _draggedIndex = null;

    private async Task OnCardDrop(int targetIndex)
    {
        if (_draggedIndex is not int sourceIndex || sourceIndex == targetIndex)
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
        var pref = await Db.DashboardCardPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

        if (pref is null)
        {
            Db.DashboardCardPreferences.Add(new DashboardCardPreference { UserId = userId, CardOrder = order });
        }
        else
        {
            pref.CardOrder = order;
        }

        await Db.SaveChangesAsync();
    }
}
