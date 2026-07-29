using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Computers;

public partial class Detail : ComponentBase
{
    private enum Tab { General, Components }

    [Parameter]
    public int ComputerId { get; set; }

    [Inject]
    private GlpiNgDbContext Db { get; set; } = null!;

    private Computer? _computer;
    private Tab _activeTab = Tab.General;

    protected override async Task OnInitializedAsync()
    {
        _computer = await Db.Computers
            .AsNoTracking()
            .Include(computer => computer.Components)
            .FirstOrDefaultAsync(computer => computer.Id == ComputerId);
    }

    private void SetTab(Tab tab)
    {
        _activeTab = tab;
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

    private static string ComponentTypeLabel(ComponentType type) => type switch
    {
        ComponentType.Cpu => "Processeur",
        ComponentType.Ram => "Mémoire",
        ComponentType.Disk => "Disque",
        ComponentType.NetworkCard => "Carte réseau",
        ComponentType.Gpu => "Carte graphique",
        ComponentType.Motherboard => "Carte mère",
        _ => type.ToString()
    };
}
