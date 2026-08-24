using System.Security.Claims;
using BlazorBootstrap;
using GlpiNg.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.MyAccount;

/// <summary>
/// Préférences personnelles de l'utilisateur connecté (menu utilisateur de la topbar — voir
/// MainLayout.razor). Le repli du menu latéral (GlpiUser.SidebarCollapsed) est aussi modifiable
/// directement depuis la barre latérale (MainLayout.ToggleSidebarCollapse) : cette page en est le
/// pendant explicite, avec un bouton "Sauvegarder" comme les autres formulaires de l'application.
/// Un rechargement complet (forceLoad) suit l'enregistrement pour que MainLayout, qui ne lit
/// SidebarCollapsed qu'une fois à l'initialisation du circuit, reflète immédiatement le changement.
/// </summary>
public partial class Preferences : ComponentBase, IAsyncDisposable
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private GlpiNgDbContext? _db;
    private int? _userId;
    private bool _sidebarCollapsed;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            string? userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _userId = int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        if (_userId is not int uid)
        {
            return;
        }

        _db = await DbFactory.CreateDbContextAsync();
        _sidebarCollapsed = await _db.Users.AsNoTracking()
            .Where(u => u.Id == uid)
            .Select(u => u.SidebarCollapsed)
            .FirstOrDefaultAsync();
    }

    private async Task SaveAsync()
    {
        if (_db is null || _userId is not int uid)
        {
            return;
        }

        _isSaving = true;

        try
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid);
            if (user is null)
            {
                return;
            }

            user.SidebarCollapsed = _sidebarCollapsed;
            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, "Préférences enregistrées."));
            Nav.NavigateTo(Nav.Uri, forceLoad: true);
        }
        finally
        {
            _isSaving = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
