using System.Security.Cryptography;
using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Localization;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using GlpiNg.Web.Controllers;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace GlpiNg.Web.Components.Pages.Deployments;

/// <summary>
/// « Installation de l'agent » : le fichier d'installation de GLPI-Agent et les options qui
/// seront passées à l'installeur. La page jumelle <see cref="AgentDeploy"/> consomme ces
/// réglages pour installer effectivement l'agent sur des postes.
/// </summary>
public partial class AgentInstall : ComponentBase
{
    /// <summary>300 Mo : l'installeur de GLPI-Agent pèse quelques dizaines de Mo, la marge couvre un paquet personnalisé.</summary>
    private const long MaxUploadBytes = 300L * 1024 * 1024;

    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [Inject]
    private ConfigHistoryService History { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [Inject]
    private AgentInstallService InstallService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private AgentInstallSettings? _model;
    private bool _isSaving;
    private bool _isUploading;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        _model = await SettingsStore.ReadSectionAsync<AgentInstallSettings>(AgentInstallerController.SectionName);

        if (string.IsNullOrWhiteSpace(_model.ServerUrl))
        {
            // Par défaut, ce serveur-ci : c'est le cas courant, et une URL vide produirait un
            // agent installé mais ne sachant où remonter son inventaire.
            _model.ServerUrl = new Uri(new Uri(Navigation.BaseUri), "inventory").ToString();
        }
    }

    /// <summary>URL publique de l'installeur — voir <see cref="AgentInstallerController"/>.</summary>
    private string InstallerUrl()
        => _model is null || string.IsNullOrEmpty(_model.DownloadToken)
            ? string.Empty
            : new Uri(new Uri(Navigation.BaseUri), $"inventory/agent-installer/{_model.DownloadToken}").ToString();

    private string Script()
        => _model is null ? string.Empty : AgentInstallService.BuildScript(_model, new Uri(InstallerUrl()));

    private async Task UploadAsync(InputFileChangeEventArgs args)
    {
        if (_model is null)
        {
            return;
        }

        _isUploading = true;
        _error = null;

        try
        {
            IBrowserFile file = args.File;
            await using Stream content = file.OpenReadStream(MaxUploadBytes);

            _model.InstallerSizeBytes = await InstallService.SaveInstallerAsync(content, file.Name);
            _model.InstallerFileName = file.Name;
            // Nouveau fichier, nouveau jeton : l'ancienne URL cesse de servir quoi que ce soit.
            _model.DownloadToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

            await SettingsStore.SaveSectionAsync(AgentInstallerController.SectionName, _model);
            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("« {0} » téléversé.", file.Name)));
        }
        catch (IOException ex)
        {
            _error = Tr.T("Fichier refusé (taille maximale {0} Mo) : {1}", MaxUploadBytes / (1024 * 1024), ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            _error = Tr.T("Échec du téléversement : {0}", ex.Message);
        }
        finally
        {
            _isUploading = false;
        }
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
            AgentInstallSettings before = await SettingsStore.ReadSectionAsync<AgentInstallSettings>(AgentInstallerController.SectionName);
            await SettingsStore.SaveSectionAsync(AgentInstallerController.SectionName, _model);
            await History.AppendAsync("Administrateur", SettingsDiff.Compare(before, _model));
            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Configuration enregistrée.")));
        }
        catch (IOException ex)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Échec de l'enregistrement : {0}", ex.Message)));
        }
        finally
        {
            _isSaving = false;
        }
    }
}
