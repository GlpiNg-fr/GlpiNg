using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages;

public partial class Login : ComponentBase
{
    [Inject]
    private AppSettingsFileStore SettingsStore { get; set; } = null!;

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrlParam { get; set; }

    private GeneralSettings? _general;
    private List<AuthLdapServer> _ldapServers = [];

    private bool HasError => Error == "1";

    private string ReturnUrl => string.IsNullOrWhiteSpace(ReturnUrlParam) ? "/" : ReturnUrlParam;

    protected override async Task OnInitializedAsync()
    {
        _general = await SettingsStore.ReadGeneralAsync();

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
}
