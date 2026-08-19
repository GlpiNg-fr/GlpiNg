using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Logs;

public partial class Index : ComponentBase
{
    /// <summary>Aligné sur ConfigHistoryService.MaxEntries : borne la page plutôt que de paginer côté serveur.</summary>
    private const int MaxEntries = 500;

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    private List<EventLogEntry> _entries = [];
    private List<EventLogEntry> _filteredEntries = [];
    private List<string> _services = [];
    private string _searchTerm = string.Empty;
    private string _serviceFilter = string.Empty;
    private string _levelFilter = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _entries = await db.EventLogEntries
            .AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Take(MaxEntries)
            .ToListAsync();

        _services = _entries.Select(e => e.Service).Distinct().OrderBy(s => s).ToList();
        ApplyFilter();
    }

    private async Task OnRefreshAsync()
    {
        await LoadAsync();
    }

    private void OnFilterChanged(KeyboardEventArgs args)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<EventLogEntry> query = _entries;

        if (!string.IsNullOrEmpty(_serviceFilter))
        {
            query = query.Where(e => e.Service == _serviceFilter);
        }

        if (!string.IsNullOrEmpty(_levelFilter) && Enum.TryParse(_levelFilter, out EventLogLevel level))
        {
            query = query.Where(e => e.Level == level);
        }

        string term = _searchTerm.Trim();
        if (term.Length > 0)
        {
            query = query.Where(e => MatchesSearch(e, term));
        }

        _filteredEntries = query.ToList();
    }

    private static bool MatchesSearch(EventLogEntry entry, string term)
    {
        return entry.Message.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (entry.ItemLabel?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string LevelLabel(EventLogLevel level) => level switch
    {
        EventLogLevel.Critical => "Critique",
        EventLogLevel.Error => "Erreur",
        EventLogLevel.Warning => "Avertissement",
        _ => "Information",
    };

    private static string LevelBadgeCss(EventLogLevel level) => level switch
    {
        EventLogLevel.Critical => "bg-red",
        EventLogLevel.Error => "bg-red-lt",
        EventLogLevel.Warning => "bg-yellow-lt",
        _ => "bg-blue-lt",
    };

    /// <summary>Lien vers la fiche de l'élément concerné, pour les quelques itemtypes que le journal connaît (voir EventLogService).</summary>
    private static string? ItemUrl(string? itemType, int? itemId) => (itemType, itemId) switch
    {
        (nameof(GlpiUser), { } id) => $"/admin/users/{id}",
        ("GlpiAgent", { } id) => $"/tools/deployments/agent/{id}",
        _ => null,
    };
}
