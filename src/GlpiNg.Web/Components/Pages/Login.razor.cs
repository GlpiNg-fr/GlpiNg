using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;

namespace GlpiNg.Web.Components.Pages;

public partial class Login : ComponentBase
{
    [Inject]
    private AppSettingsFileStore SettingsStore { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }

    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrlParam { get; set; }

    private GeneralSettings? _general;

    private bool HasError => Error == "1";

    private string ReturnUrl => string.IsNullOrWhiteSpace(ReturnUrlParam) ? "/" : ReturnUrlParam;

    protected override async Task OnInitializedAsync()
    {
        _general = await SettingsStore.ReadGeneralAsync();
    }
}
