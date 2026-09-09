using System.Security.Claims;
using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Preferences;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
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

    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private const int DefaultItemsPerPage = 25;

    /// <summary>Mêmes valeurs que le sélecteur au pied de chaque liste, pour qu'un choix fait ici
    /// puisse être retrouvé là-bas.</summary>
    private static readonly int[] ItemsPerPageOptions = [25, 50, 100, 200, 500];

    private GlpiNgDbContext? _db;
    private int? _userId;
    private bool _sidebarCollapsed;
    private int _itemsPerPage = DefaultItemsPerPage;
    private MacAddressFormat _macAddressFormat = MacAddressFormat.Default;

    /// <summary>Forme retenue par la configuration générale, montrée dans l'option « Réglage de
    /// l'instance » : sans elle, ce choix n'indiquerait pas ce qu'il applique.</summary>
    private MacAddressFormat _instanceMacFormat = MacAddressFormatter.Fallback;
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

        _instanceMacFormat = MacAddressFormatter.FromSettingsValue(
            (await SettingsStore.ReadSectionAsync<GeneralSettings>("GeneralSettings")).MacAddressFormat);

        _db = await DbFactory.CreateDbContextAsync();

        var stored = await _db.Users.AsNoTracking()
            .Where(u => u.Id == uid)
            .Select(u => new { u.SidebarCollapsed, u.ItemsPerPage, u.MacAddressFormat })
            .FirstOrDefaultAsync();

        if (stored is not null)
        {
            _sidebarCollapsed = stored.SidebarCollapsed;
            // 0 = rien choisi (voir GlpiUser.ItemsPerPage) : le sélecteur doit alors montrer la
            // valeur réellement appliquée, pas une entrée vide.
            _itemsPerPage = stored.ItemsPerPage > 0 ? stored.ItemsPerPage : DefaultItemsPerPage;
            _macAddressFormat = stored.MacAddressFormat;
        }
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
            user.ItemsPerPage = _itemsPerPage;
            user.MacAddressFormat = _macAddressFormat;
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
