using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Computers;

public partial class Index : ComponentBase
{
    private enum SortField
    {
        Name,
        Status,
        Manufacturer,
        OperatingSystem,
        LastInventory
    }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    private List<Computer> _computers = [];
    private List<Computer> _filteredComputers = [];
    private string _searchTerm = string.Empty;
    private SortField _sortField = SortField.Name;
    private bool _sortDescending;
    private readonly HashSet<int> _selectedIds = [];

    private bool AllSelected => _filteredComputers.Count > 0 && _selectedIds.Count == _filteredComputers.Count;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _computers = await db.Computers
            .AsNoTracking()
            .OrderBy(computer => computer.Name)
            .ToListAsync();

        _selectedIds.Clear();
        ApplyFilterAndSort();
    }

    private async Task OnRefreshAsync()
    {
        await LoadAsync();
    }

    private void OnSearchChanged(KeyboardEventArgs args)
    {
        ApplyFilterAndSort();
    }

    private void SetSort(SortField field)
    {
        if (_sortField == field)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortField = field;
            _sortDescending = false;
        }

        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        string term = _searchTerm.Trim();

        IEnumerable<Computer> query = term.Length == 0
            ? _computers
            : _computers.Where(computer => MatchesSearch(computer, term));

        query = _sortField switch
        {
            SortField.Status => OrderBy(query, computer => computer.Status),
            SortField.Manufacturer => OrderBy(query, ManufacturerAndModel),
            SortField.OperatingSystem => OrderBy(query, computer => computer.OperatingSystem ?? string.Empty),
            SortField.LastInventory => OrderBy(query, computer => computer.LastInventoryAt ?? DateTimeOffset.MinValue),
            _ => OrderBy(query, computer => computer.Name)
        };

        _filteredComputers = query.ToList();
        _selectedIds.IntersectWith(_filteredComputers.Select(computer => computer.Id));
    }

    private IOrderedEnumerable<Computer> OrderBy<TKey>(IEnumerable<Computer> query, Func<Computer, TKey> keySelector)
    {
        return _sortDescending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (Computer computer in _filteredComputers)
            {
                _selectedIds.Add(computer.Id);
            }
        }
    }

    private void ToggleSelect(int computerId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(computerId);
        }
        else
        {
            _selectedIds.Remove(computerId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0) return;

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<Computer> toDelete = await db.Computers
            .Where(computer => _selectedIds.Contains(computer.Id))
            .ToListAsync();

        db.Computers.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private static bool MatchesSearch(Computer computer, string term)
    {
        return computer.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (computer.Manufacturer?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (computer.Model?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (computer.OperatingSystem?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string ManufacturerAndModel(Computer computer)
    {
        bool hasManufacturer = !string.IsNullOrWhiteSpace(computer.Manufacturer);
        bool hasModel = !string.IsNullOrWhiteSpace(computer.Model);

        if (!hasManufacturer && !hasModel) return "—";
        if (hasManufacturer && hasModel) return $"{computer.Manufacturer} {computer.Model}";
        return hasManufacturer ? computer.Manufacturer! : computer.Model!;
    }

    private static string LastInventoryLabel(Computer computer)
    {
        return computer.LastInventoryAt is { } lastInventory
            ? lastInventory.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "Jamais";
    }

    private static string StatusLabel(ComputerStatus status) => status switch
    {
        ComputerStatus.InStock => "En stock",
        ComputerStatus.InProduction => "En production",
        ComputerStatus.Broken => "En panne",
        ComputerStatus.Retired => "Réformé",
        _ => status.ToString()
    };

    private static string StatusCssClass(ComputerStatus status) => status switch
    {
        ComputerStatus.InStock => "glpi-status-instock",
        ComputerStatus.InProduction => "glpi-status-inproduction",
        ComputerStatus.Broken => "glpi-status-broken",
        ComputerStatus.Retired => "glpi-status-retired",
        _ => "bg-secondary"
    };
}
