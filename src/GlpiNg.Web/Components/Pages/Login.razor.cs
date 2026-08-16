using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages;

public partial class Login : ComponentBase
{
    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrlParam { get; set; }

    private GeneralSettings? _general;
    private List<AuthLdapServer> _ldapServers = [];

    private string ReturnUrl => string.IsNullOrWhiteSpace(ReturnUrlParam) ? "/" : ReturnUrlParam;

    protected override async Task OnInitializedAsync()
    {
        _general = await SettingsStore.ReadSectionAsync<GeneralSettings>("GeneralSettings");

        if (_general.ShowAuthSourcesOnLoginPage)
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
            _ldapServers = await db.AuthLdapServers.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.IsDefault)
                .ThenBy(s => s.Name)
                .ToListAsync();
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        // Notify() must run after the interactive circuit is attached (not during
        // static prerendering, when no live Toasts component/SignalR connection
        // exists yet) so the toast is actually visible to the user.
        if (firstRender && Error == "1")
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Nom d'utilisateur ou mot de passe incorrect."));
        }

        return Task.CompletedTask;
    }
}
