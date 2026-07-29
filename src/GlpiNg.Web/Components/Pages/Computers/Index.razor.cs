using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Computers;

public partial class Index : ComponentBase
{
    [Inject]
    private GlpiNgDbContext Db { get; set; } = null!;

    private List<Computer> _computers = [];
    private List<Computer> _filteredComputers = [];
    private string _searchTerm = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _computers = await Db.Computers
            .AsNoTracking()
            .OrderBy(computer => computer.Name)
            .ToListAsync();

        _filteredComputers = _computers;
    }

    private void OnSearchChanged(KeyboardEventArgs args)
    {
        string term = _searchTerm.Trim();

        _filteredComputers = term.Length == 0
            ? _computers
            : _computers.Where(computer => MatchesSearch(computer, term)).ToList();
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
