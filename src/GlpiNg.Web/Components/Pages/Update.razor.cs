using GlpiNg.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages;

public partial class Update : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    private List<string> _pendingMigrations = [];
    private bool _applying;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
    }

    private async Task ApplyAsync()
    {
        _applying = true;
        _error = null;

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
            Nav.NavigateTo("/", forceLoad: true);
        }
        catch (Exception ex)
        {
            _error = ex.Message;
            _applying = false;
        }
    }

    private void GoHome() => Nav.NavigateTo("/", forceLoad: true);
}
