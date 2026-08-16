using BlazorBootstrap;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;

namespace GlpiNg.Web.Components.Pages.Deployments;

public partial class GeneralConfig : ComponentBase
{
    private const string SectionName = "DeploymentGeneralSettings";

    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [Inject]
    private ConfigHistoryService History { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private DeploymentGeneralSettings? _model;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        _model = await SettingsStore.ReadSectionAsync<DeploymentGeneralSettings>(SectionName);
    }

    private async Task SaveAsync()
    {
        if (_model is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            DeploymentGeneralSettings before = await SettingsStore.ReadSectionAsync<DeploymentGeneralSettings>(SectionName);
            await SettingsStore.SaveSectionAsync(SectionName, _model);
            await History.AppendAsync("Administrateur", SettingsDiff.Compare(before, _model));
            ToastService.Notify(new ToastMessage(ToastType.Success, "Configuration enregistrée."));
        }
        catch (IOException ex)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, $"Échec de l'enregistrement : {ex.Message}"));
        }
        finally
        {
            _isSaving = false;
        }
    }
}
